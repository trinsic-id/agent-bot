// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using AgentBot.Dialogs;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace AgentBot
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service.  Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class AgentBotBot : IBot
    {
        private readonly AgentBotAccessors _accessors;
        private readonly DialogSet _dialogSet;
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly BotServices _botServices;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="conversationState">The managed conversation state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
        public AgentBotBot(Microsoft.Bot.Builder.ConversationState conversationState, IServiceProvider serviceProvider, ILoggerFactory loggerFactory, BotServices botServices)
        {
            if (conversationState == null)
            {
                throw new System.ArgumentNullException(nameof(conversationState));
            }

            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _accessors = new AgentBotAccessors(conversationState)
            {
                AgentState = conversationState.CreateProperty<AgentState>(AgentBotAccessors.AgentStateName),
                DialogState = conversationState.CreateProperty<DialogState>(AgentBotAccessors.DialogStateName)
            };

            _dialogSet = new DialogSet(_accessors.DialogState);
            _dialogSet.Add(new ProvisionAgentDialog("provision-agent", serviceProvider, _accessors));
            _dialogSet.Add(new CreateInvitationDialog("create-invitation", serviceProvider, _accessors));
            _dialogSet.Add(new TextPrompt("text-prompt"));
            _dialogSet.Add(new ConfirmPrompt("yes-no-prompt"));

            _logger = loggerFactory.CreateLogger<AgentBotBot>();
            _logger.LogTrace("Turn start.");
            _serviceProvider = serviceProvider;
            _botServices = botServices;
        }

        /// <summary>
        /// Every conversation turn for our Echo Bot will call this method.
        /// There are no dialogs used, since it's "single turn" processing, meaning a single
        /// request and response.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        /// <seealso cref="BotStateSet"/>
        /// <seealso cref="Microsoft.Bot.Builder.ConversationState"/>
        /// <seealso cref="IMiddleware"/>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Handle Message activity type, which is the main activity type for shown within a conversational interface
            // Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
            // see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                var context = await _dialogSet.CreateContextAsync(turnContext, cancellationToken);
                var results = await context.ContinueDialogAsync(cancellationToken);

                // Get the conversation state from the turn context.
                var state = await _accessors.AgentState.GetAsync(turnContext, () => new AgentState(), cancellationToken);


                // If the DialogTurnStatus is Empty we should start a new dialog.
                if (results.Status == DialogTurnStatus.Empty)
                {
                    // Check LUIS model
                    var recognizerResult = await _botServices.LuisServices["AgentBot"].RecognizeAsync(turnContext, cancellationToken);
                    var topIntent = recognizerResult?.GetTopScoringIntent();

                    if (topIntent.HasValue && topIntent.Value.score > 0.7)
                    {
                        switch (topIntent.Value.intent)
                        {
                            case "Agent_Provision":
                                {
                                    var result = await context.BeginDialogAsync("provision-agent", null, cancellationToken);
                                    break;
                                }
                            case "Connection_CreateInvitation":
                                {
                                    var result = await context.BeginDialogAsync("create-invitation", null, cancellationToken);
                                    break;
                                }
                            default:
                                {
                                    await turnContext.SendActivityAsync($"I can't process this intent yet ({topIntent.Value.intent})");
                                    break;
                                }
                        }
                    }
                    else
                    {
                        state.TurnCount++;
                        await _accessors.AgentState.SetAsync(turnContext, state, cancellationToken);
                        var responseMessage = $"Turn {state.TurnCount}: You sent '{turnContext.Activity.Text}'\n";
                        await turnContext.SendActivityAsync(responseMessage, cancellationToken: cancellationToken);
                    }
                }
                await _accessors.ConversationState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (turnContext.Activity.MembersAdded != null)
                {
                    // Iterate over all new members added to the conversation
                    foreach (var member in turnContext.Activity.MembersAdded)
                    {
                        // Greet anyone that was not the target (recipient) of this message
                        // the 'bot' is the recipient for events from the channel,
                        // turnContext.Activity.MembersAdded == turnContext.Activity.Recipient.Id indicates the
                        // bot was added to the conversation.
                        if (member.Id != turnContext.Activity.Recipient.Id)
                        {
                            await turnContext.SendActivityAsync($"Hi there!");
                        }
                    }
                }
            }
            else
            {
                await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected", cancellationToken: cancellationToken);
            }
        }
    }
}
