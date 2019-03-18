using System;
using System.Threading;
using System.Threading.Tasks;
using AgentBot.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        /// <summary>
        /// Uses the agent framework bot.
        /// </summary>
        /// <param name="app">App.</param>
        public static IApplicationBuilder UseAgentFrameworkBot(this IApplicationBuilder app)
        {
            var agentOptions = app.ApplicationServices.GetRequiredService<IOptions<AgentOptions>>().Value;

            app.MapWhen(
                context => context.Request.Path.Value.StartsWith(
                    new Uri(agentOptions.EndpointHost).AbsolutePath, StringComparison.Ordinal),
                appBuilder => { appBuilder.UseMiddleware<AgentBotMiddleware>(); });

            return app;
        }
    }
}