using System;
using System.Threading.Tasks;
using AgentFramework.Core.Contracts;
using AgentFramework.Core.Handlers;
using AgentFramework.Core.Models;
using AgentFramework.Core.Models.Wallets;

namespace AgentBot.Services
{
    public class AgentContextProvider : IAgentContextProvider
    {
        private readonly IWalletService _walletService;

        public AgentContextProvider(IWalletService walletService)
        {
            _walletService = walletService;
        }

        public async Task<IAgentContext> GetContextAsync(string agentId = null)
        {
            if (agentId == null)
            {
                throw new ArgumentNullException(nameof(agentId));
            }

            return new AgentContext
            {
                Wallet = await _walletService.GetWalletAsync(
                    new WalletConfiguration { Id = agentId },
                    new WalletCredentials { Key = "DefaultKey" }),
                Pool = new PoolAwaitable(() => throw new NotImplementedException("This agent doesn't support pool operations"))
            };
        }
    }
}
