using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Karamem0.BookingServiceBot.Models
{

    public class GraphServiceClientProvider
    {

        public GraphServiceClient Value { get; }

        public GraphServiceClientProvider(IOptions<GraphServiceOptions> options)
        {
            var application = PublicClientApplicationBuilder
                .Create(options.Value.ClientId)
                .WithTenantId(options.Value.TenantId)
                .Build();
            var credentials = new NetworkCredential(options.Value.UserName, options.Value.Password);
            var provider = new UsernamePasswordProvider(application, credentials);
            this.Value = new GraphServiceClient(provider);
        }

        private class UsernamePasswordProvider : IAuthenticationProvider
        {

            private readonly IPublicClientApplication application;

            private readonly NetworkCredential credentials;

            private AuthenticationResult authenticationResult;

            public UsernamePasswordProvider(IPublicClientApplication application, NetworkCredential credentials)
            {
                this.application = application;
                this.credentials = credentials;
            }

            public async Task AuthenticateRequestAsync(HttpRequestMessage request)
            {
                if (this.authenticationResult == null ||
                    this.authenticationResult.ExpiresOn <= DateTime.UtcNow)
                {
                    this.authenticationResult =
                        await application
                            .AcquireTokenByUsernamePassword(
                                null,
                                this.credentials.UserName,
                                this.credentials.SecurePassword)
                            .ExecuteAsync();
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.authenticationResult.AccessToken);
            }

        }

    }

}
