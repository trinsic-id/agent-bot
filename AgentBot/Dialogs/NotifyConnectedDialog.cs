using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFramework.Core.Contracts;
using AgentFramework.Core.Models.Events;
using AgentFramework.Core.Models.Records;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentBot.Dialogs
{
    public class NotifyConnectedDialog : Dialog
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IConnectionService _connectionService;
        private readonly IAgentContextProvider _agentContextProvider;
        private readonly AgentBotAccessors _accessors;
        private readonly ILogger<NotifyConnectedDialog> _logger;

        public NotifyConnectedDialog(string dialogId, IServiceProvider provider, AgentBotAccessors accessors, string appId) : base(dialogId)
        {
            _eventAggregator = provider.GetRequiredService<IEventAggregator>();
            _connectionService = provider.GetRequiredService<IConnectionService>();
            _agentContextProvider = provider.GetRequiredService<IAgentContextProvider>();
            _logger = provider.GetRequiredService<ILogger<NotifyConnectedDialog>>();
            _accessors = accessors;

            AppId = appId;
        }

        public string AppId { get; }

        public override async Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (options is ServiceMessageProcessingEvent messageEvent)
            {
                var state = await _accessors.AgentState.GetAsync(dc.Context, () => new AgentState(), cancellationToken);
                if (state.WalletId != null)
                {
                    _eventAggregator.GetEventByType<ServiceMessageProcessingEvent>()
                        .Where(x => x.MessageType == messageEvent.MessageType && x.RecordId == messageEvent.RecordId)
                        .Subscribe(CreateCallback(dc.Context.Adapter, dc.Context.Activity.GetConversationReference(),
                            state.WalletId, messageEvent.RecordId));
                }
                else
                {
                    _logger.LogWarning("Notify connected requested, but no agent data found in state");
                }
            }
            return await dc.EndDialogAsync();
        }

        private Action<ServiceMessageProcessingEvent> CreateCallback(BotAdapter adapter, ConversationReference conversationReference, 
            string agentId, string connectionId)
        {
            return async message =>
            {
                var agentContext = await _agentContextProvider.GetContextAsync(agentId);
                var connection = await _connectionService.GetAsync(agentContext, connectionId);

                await adapter.ContinueConversationAsync(AppId, conversationReference, async (turnContext, cancellationToken) =>
                {
                    await turnContext.SendActivityAsync($"You are now connected to {connection.Alias?.Name ?? "[unspecified]"}");
                }, CancellationToken.None);
            };
        }
    }
}
