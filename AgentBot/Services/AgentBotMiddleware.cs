using System;
using System.IO;
using System.Threading.Tasks;
using AgentFramework.Core.Contracts;
using AgentFramework.Core.Extensions;
using AgentFramework.Core.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBot.Services
{
    public class AgentBotMiddleware : AgentMessageProcessorBase
    {
        private readonly RequestDelegate _next;
        private readonly IAgentContextProvider _contextProvider;

        public AgentBotMiddleware(
            RequestDelegate next,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _next = next;
            _contextProvider = serviceProvider.GetRequiredService<IAgentContextProvider>();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (context.Request.ContentLength == null) throw new Exception("Empty content length");

            using (var stream = new StreamReader(context.Request.Body))
            {
                var body = await stream.ReadToEndAsync();
                var agentId = context.Request.Path.Value.Replace("/", string.Empty);

                var result = await ProcessAsync(
                    body: body.GetUTF8Bytes(),
                    context: await _contextProvider.GetContextAsync(agentId));

                context.Response.StatusCode = 200;

                if (result != null)
                    await context.Response.Body.WriteAsync(result, 0, result.Length);
                else
                    await context.Response.WriteAsync(string.Empty);
            }
        }
    }
}
