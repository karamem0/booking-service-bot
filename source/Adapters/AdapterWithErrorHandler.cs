using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Karamem0.BookingServiceBot.Adapters
{

    public class ErrorHandleAdapter : BotFrameworkHttpAdapter
    {

        public ErrorHandleAdapter(IConfiguration configuration, ILogger<BotFrameworkHttpAdapter> logger) : base(configuration, logger)
        {
            this.OnTurnError = async (context, exception) =>
            {
                logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");
                await context.SendActivityAsync("The bot encountered an error or bug.");
                await context.SendActivityAsync("To continue to run this bot, please fix the bot source code.");
                await context.TraceActivityAsync("OnTurnError Trace", exception.Message, "https://www.botframework.com/schemas/error", "TurnError");
            };
        }

    }

}
