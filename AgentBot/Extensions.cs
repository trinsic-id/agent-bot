using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace AgentBot
{
    public static class Extensions
    {
        public static Task<ResourceResponse> SendTypingAsync(this ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            var typing = turnContext.Activity.CreateReply();
            typing.Type = "typing";
            return turnContext.SendActivityAsync(typing, cancellationToken);
        }
    }
}
