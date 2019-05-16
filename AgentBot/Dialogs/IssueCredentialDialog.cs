using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;

namespace AgentBot.Dialogs
{
    public class IssueCredentialDialog : WaterfallDialog
    {
        public IssueCredentialDialog(string dialogId) : base(dialogId)
        {
            AddStep(SelectCredentialAsync);
        }

        private async Task<DialogTurnResult> SelectCredentialAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync("credential-type-prompt", new PromptOptions
            {
                Prompt = MessageFactory.Text("What type of credential would you like?"),
                Choices = new List<Choice>
                {
                    new Choice("Email"),
                    new Choice("Phone"),
                    new Choice("Twitter")
                },
                Style = ListStyle.SuggestedAction
            });
        }
    }
}
