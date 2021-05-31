using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;

namespace Karamem0.BookingServiceBot.Controllers
{

    [ApiController()]
    [Route("api/messages")]
    public class BotController : ControllerBase
    {

        private readonly IBotFrameworkHttpAdapter adapter;

        private readonly IBot bot;

        public BotController(IBotFrameworkHttpAdapter adapter, IBot bot)
        {
            this.adapter = adapter;
            this.bot = bot;
        }

        [HttpGet()]
        [HttpPost()]
        public async Task PostAsync()
        {
            await this.adapter.ProcessAsync(Request, Response, this.bot);
        }

    }

}
