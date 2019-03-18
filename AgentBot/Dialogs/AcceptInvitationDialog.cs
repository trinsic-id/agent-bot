using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgentFramework.Core.Contracts;
using AgentFramework.Core.Messages;
using AgentFramework.Core.Messages.Connections;
using AgentFramework.Core.Models.Events;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AgentBot.Dialogs
{
    public class AcceptInvitationDialog : WaterfallDialog
    {
        private readonly AgentBotAccessors _accessors;
        private readonly IConnectionService _connectionService;
        private readonly IMessageService _messageService;
        private readonly IAgentContextProvider _contextProvider;

        public AcceptInvitationDialog(string dialogId, IServiceProvider serviceProvider, AgentBotAccessors accessors) : base(dialogId)
        {
            _accessors = accessors;
            _connectionService = serviceProvider.GetRequiredService<IConnectionService>();
            _messageService = serviceProvider.GetRequiredService<IMessageService>();
            _contextProvider = serviceProvider.GetRequiredService<IAgentContextProvider>();

            AddStep(ParseInvitationAsync);
            AddStep(ParseFollowUpAsync);
            AddStep(CheckAgentAsync);
            AddStep(ProvisionAgentAsync);
            AddStep(AcceptInvitationAsync);
            AddStep(RegisterNotifyAsync);
        }

        private async Task<DialogTurnResult> ParseInvitationAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (Uri.TryCreate(stepContext.Options?.ToString(), UriKind.Absolute, out var invitationUri))
            {
                await stepContext.Context.SendActivityAsync($"It appears you received an invitation to connect from {ParseLabel(invitationUri)}.");
                return await stepContext.PromptAsync("yes-no-prompt",
                    new PromptOptions { Prompt = MessageFactory.Text("Would you like to accept it?") });
            }

            await stepContext.Context.SendActivityAsync("I couldn't find an invitation in that URL");
            return await stepContext.EndDialogAsync(false);
        }

        private async Task<DialogTurnResult> ParseFollowUpAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (bool.TryParse(stepContext.Result?.ToString(), out var accept) && accept)
            {
                return await stepContext.NextAsync();
            }
            await stepContext.Context.SendActivityAsync("Ok");
            return await stepContext.EndDialogAsync(false);
        }

        private async Task<DialogTurnResult> CheckAgentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var state = await _accessors.AgentState.GetAsync(stepContext.Context, () => new AgentState(), cancellationToken);

            if (state.WalletId == null)
            {
                await stepContext.Context.SendActivityAsync("You must provision an agent before accepting invitations");
                return await stepContext.PromptAsync("yes-no-prompt",
                    new PromptOptions { Prompt = MessageFactory.Text("Would you like to do that now?") });
            }
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> ProvisionAgentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Result == null)
            {
                return await stepContext.NextAsync();
            }
            if (bool.TryParse(stepContext.Result.ToString(), out var answer) && answer)
            {
                return await stepContext.BeginDialogAsync("provision-agent");
            }
            await stepContext.Context.SendActivityAsync("Ok");
            return await stepContext.EndDialogAsync();
        }

        private async Task<DialogTurnResult> AcceptInvitationAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (Uri.TryCreate(stepContext.Options?.ToString(), UriKind.Absolute, out var invitationUri))
            {
                await stepContext.Context.SendTypingAsync();

                string invitationDecoded = null;
                var query = QueryHelpers.ParseQuery(invitationUri.Query);
                if (query.TryGetValue("c_i", out var invitationEncoded))
                {
                    invitationDecoded = Uri.UnescapeDataString(invitationEncoded);
                }

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(invitationDecoded));
                var jobj = JObject.Parse(json);

                switch (jobj["@type"].Value<string>())
                {
                    case MessageTypes.ConnectionInvitation:
                        {
                            var state = await _accessors.AgentState.GetAsync(stepContext.Context, () => new AgentState(), cancellationToken);
                            if (state.WalletId != null)
                            {
                                var invitation = JsonConvert.DeserializeObject<ConnectionInvitationMessage>(json);
                                var agentContext = await _contextProvider.GetContextAsync(state.WalletId);

                                var (message, record) = await _connectionService.CreateRequestAsync(agentContext, invitation);
                                await _messageService.SendToConnectionAsync(agentContext.Wallet, message, record,
                                    invitation.RecipientKeys.First());

                                await stepContext.Context.SendActivityAsync("I accepted the invitation and initiated connection");

                                return await stepContext.NextAsync(record.Id);
                            }
                            break;
                        }
                }
            }
            // End the dialog and notify the flow hasn't been successful for whatever reason
            return await stepContext.EndDialogAsync(false);
        }

        private Task<DialogTurnResult> RegisterNotifyAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return stepContext.BeginDialogAsync("notify-connected", new ServiceMessageProcessingEvent
            {
                MessageType = MessageTypes.ConnectionResponse,
                RecordId = stepContext.Result.ToString()
            });
        }

        private string ParseLabel(Uri invitationUri)
        {
            string invitationDecoded = null;
            var query = QueryHelpers.ParseQuery(invitationUri.Query);
            if (query.TryGetValue("c_i", out var invitationEncoded))
            {
                invitationDecoded = Uri.UnescapeDataString(invitationEncoded);
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(invitationDecoded));
            var jobj = JObject.Parse(json);

            switch (jobj["@type"].Value<string>())
            {
                case MessageTypes.ConnectionInvitation:
                    {
                        var invitation = JsonConvert.DeserializeObject<ConnectionInvitationMessage>(json);
                        return invitation.Label ?? "[unspecified]";
                    }
            }
            return "[unspecified]";
        }
    }
}
