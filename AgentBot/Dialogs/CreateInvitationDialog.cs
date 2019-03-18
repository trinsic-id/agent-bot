using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFramework.Core.Contracts;
using AgentFramework.Core.Extensions;
using AgentFramework.Core.Messages;
using AgentFramework.Core.Models.Connections;
using AgentFramework.Core.Models.Events;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBot.Dialogs
{
    public class CreateInvitationDialog : WaterfallDialog
    {
        private readonly AgentBotAccessors _accessors;
        private readonly IAgentContextProvider _contextProvider;
        private readonly IConnectionService _connectionService;
        private readonly IProvisioningService _provisioningService;
        private readonly AgentOptions _agentOptions;

        public CreateInvitationDialog(string dialogId, IServiceProvider serviceProvider, AgentBotAccessors accessors) : base(dialogId)
        {
            _accessors = accessors;
            _contextProvider = serviceProvider.GetRequiredService<IAgentContextProvider>();
            _connectionService = serviceProvider.GetRequiredService<IConnectionService>();
            _provisioningService = serviceProvider.GetRequiredService<IProvisioningService>();
            _agentOptions = serviceProvider.GetService<IOptions<AgentOptions>>().Value;

            AddStep(CheckAgentAsync);
            AddStep(ProvisionAgentAsync);
            AddStep(CreateInvitationAsync);
            AddStep(RegisterNotifyAsync);
        }

        private async Task<DialogTurnResult> CheckAgentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var state = await _accessors.AgentState.GetAsync(stepContext.Context, () => new AgentState(), cancellationToken);

            if (state.WalletId == null)
            {
                await stepContext.Context.SendActivityAsync("You must provision an agent before creating invitations");
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

        private async Task<DialogTurnResult> CreateInvitationAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var state = await _accessors.AgentState.GetAsync(stepContext.Context, () => new AgentState(), cancellationToken);
            if (state.WalletId == null)
            {
                await stepContext.Context.SendActivityAsync("Sorry, I couldn't find information about your agent");
                return await stepContext.EndDialogAsync();
            }
            await stepContext.Context.SendTypingAsync();

            var context = await _contextProvider.GetContextAsync(state.WalletId);
            var provisioning = await _provisioningService.GetProvisioningAsync(context.Wallet);

            var (invitation, record) = await _connectionService.CreateInvitationAsync(context,
                new InviteConfiguration { AutoAcceptConnection = true });

            var invitationDetails = $"{provisioning.Endpoint.Uri}?c_i={invitation.ToByteArray().ToBase64String()}";

            var reply = stepContext.Context.Activity.CreateReply("Here are the invitation details");
            reply.Attachments = new List<Attachment>
            {
                new HeroCard
                {
                    Images = new List<CardImage>
                    {
                        new CardImage{Url = $"https://chart.googleapis.com/chart?cht=qr&chs=300x300&chld=M|0&chl={Uri.EscapeDataString(invitationDetails)}" }
                    }
                }.ToAttachment()
            };

            await stepContext.Context.SendActivityAsync(reply);
            await stepContext.Context.SendActivityAsync(invitationDetails);

            return await stepContext.NextAsync(record.Id);
        }

        private Task<DialogTurnResult> RegisterNotifyAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return stepContext.BeginDialogAsync("notify-connected", new ServiceMessageProcessingEvent
            {
                MessageType = MessageTypes.ConnectionRequest,
                RecordId = stepContext.Result.ToString()
            });
        }
    }
}