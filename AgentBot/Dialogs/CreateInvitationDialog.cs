using System;
using System.Threading;
using System.Threading.Tasks;
using AgentFramework.Core.Contracts;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBot.Dialogs
{
    public class CreateInvitationDialog : WaterfallDialog
    {
        private readonly AgentBotAccessors _accessors;
        private readonly IWalletService _walletService;

        public CreateInvitationDialog(string dialogId, IServiceProvider serviceProvider, AgentBotAccessors accessors) : base(dialogId)
        {
            _accessors = accessors;
            _walletService = serviceProvider.GetRequiredService<IWalletService>();

            AddStep(CheckAgentAsync);
            AddStep(ProvisionAgentAsync);
            AddStep(CreateInvitationAsync);
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
            await stepContext.Context.SendActivityAsync("Here are the invitation details");
            return await stepContext.EndDialogAsync();
        }
    }
}