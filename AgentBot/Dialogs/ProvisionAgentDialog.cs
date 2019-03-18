using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AgentFramework.Core.Contracts;
using AgentFramework.Core.Models.Wallets;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBot.Dialogs
{
    public class ProvisionAgentDialog : WaterfallDialog
    {
        private readonly IProvisioningService _provisioningService;
        private readonly AgentOptions _agentOptions;
        private readonly AgentBotAccessors _accessors;

        public ProvisionAgentDialog(string dialogId, IServiceProvider serviceProvider, AgentBotAccessors accessors) : base(dialogId)
        {
            _provisioningService = serviceProvider.GetRequiredService<IProvisioningService>();
            _agentOptions = serviceProvider.GetRequiredService<IOptions<AgentOptions>>().Value;
            _accessors = accessors;

            AddStep(CheckAgentAsync);
            AddStep(PromptAgentNameAsync);
            AddStep(ProvisionAsync);
        }

        private async Task<DialogTurnResult> CheckAgentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var state = await _accessors.AgentState.GetAsync(stepContext.Context, () => new AgentState(), cancellationToken);

            if (state.WalletId != null)
            {
                await stepContext.Context.SendActivityAsync("You already have an agent provisioned.");
                return await stepContext.EndDialogAsync();
            }
            return await stepContext.NextAsync();
        }

        private Task<DialogTurnResult> PromptAgentNameAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return stepContext.PromptAsync("text-prompt",
                new PromptOptions { Prompt = MessageFactory.Text("What name would you like your agent to use?") });
        }

        private async Task<DialogTurnResult> ProvisionAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendTypingAsync(cancellationToken);

            var agentName = stepContext.Result?.ToString() ?? "Agent Chat Bot";
            var walletKey = "DefaultKey";
            var walletId = $"{Guid.NewGuid().ToString("N")}";

            try
            {
                await _provisioningService.ProvisionAgentAsync(new ProvisioningConfiguration
                {
                    WalletCredentials = new WalletCredentials { Key = walletKey },
                    WalletConfiguration = new WalletConfiguration { Id = walletId },
                    EndpointUri = new Uri(new Uri(_agentOptions.EndpointHost), walletId),
                    OwnerName = agentName
                });
            }
            catch (Exception e)
            {
                await stepContext.Context.SendActivityAsync("Sorry, I couldn't provision new agent.");
                await stepContext.Context.SendActivityAsync($"Error: {e.Message}");
                return await stepContext.EndDialogAsync();
            }

            await stepContext.Context.SendActivityAsync("Your agent is ready. Go ahead and start making connections.");

            var state = await _accessors.AgentState.GetAsync(stepContext.Context, () => new AgentState(), cancellationToken);
            // Bump the turn count for this conversation.
            state.WalletKey = walletKey;
            state.WalletId = walletId;

            // Set the property using the accessor.
            await _accessors.AgentState.SetAsync(stepContext.Context, state, cancellationToken);

            return await stepContext.EndDialogAsync();
        }
    }
}
