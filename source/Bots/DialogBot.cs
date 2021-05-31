using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Karamem0.BookingServiceBot.Dialogs;
using Karamem0.BookingServiceBot.Resources;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace Karamem0.BookingServiceBot.Bots
{

    public class DialogBot : ActivityHandler
    {

        private readonly BotState conversationState;

        private readonly BotState userState;

        private readonly DialogSet dialogs;

        public DialogBot(ConversationState conversationState, UserState userState, BookingDialog bookingDialog)
        {
            this.conversationState = conversationState;
            this.userState = userState;
            this.dialogs = new DialogSet(this.userState.CreateProperty<DialogState>(nameof(DialogState)));
            this.dialogs.Add(bookingDialog);
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var dialogContext = await dialogs.CreateContextAsync(turnContext, cancellationToken);
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                var message = turnContext.Activity.Text;
                if (message == StringResources.CancelText)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(StringResources.CancelBookingMessage));
                    await dialogContext.CancelAllDialogsAsync(cancellationToken);
                }
                else
                {
                    var result = await dialogContext.ContinueDialogAsync(cancellationToken);
                    if (result.Status == DialogTurnStatus.Empty)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text(StringResources.HelloMessage));
                        await dialogContext.BeginDialogAsync(nameof(BookingDialog), null, cancellationToken);
                    }
                }
            }
            if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(StringResources.HelloMessage));
                await dialogContext.BeginDialogAsync(nameof(BookingDialog), null, cancellationToken);
            }
            await this.conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await this.userState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

    }

}
