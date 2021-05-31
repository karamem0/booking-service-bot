using Karamem0.BookingServiceBot.Adapters;
using Karamem0.BookingServiceBot.Bots;
using Karamem0.BookingServiceBot.Dialogs;
using Karamem0.BookingServiceBot.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karamem0.BookingServiceBot
{

    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddNewtonsoftJson();
            services.AddSingleton<IBotFrameworkHttpAdapter, ErrorHandleAdapter>();
            services.AddSingleton<IStorage, MemoryStorage>();
            services.AddSingleton<UserState>();
            services.AddSingleton<ConversationState>();
            services.AddSingleton<BookingDialog>();
            services.AddSingleton<GraphServiceClientProvider>();
            services.AddTransient<IBot, DialogBot>();
            services.Configure<GraphServiceOptions>(this.Configuration.GetSection("MicrosoftGraph"));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseWebSockets();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
            // app.UseHttpsRedirection();
        }

    }

}
