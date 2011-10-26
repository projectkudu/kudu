using System;
using System.Net.Http;
using System.Text;
using Microsoft.ApplicationServer.Http.Dispatcher;

namespace Kudu.Services.Authorization
{
    public class BasicAuthorizeHandler : HttpOperationHandler<HttpRequestMessage, HttpRequestMessage>
    {
        private IUserValidator validator;

        public BasicAuthorizeHandler(IUserValidator validator)
            : base("authorizedClient")
        {
            this.validator = validator;
        }

        protected override HttpRequestMessage OnHandle(HttpRequestMessage input)
        {
            var authorizationHeader = input.Headers.Authorization;

            if (authorizationHeader != null)
            {
                byte[] encodedDataAsBytes = Convert.FromBase64String(authorizationHeader.Parameter);
                string value = Encoding.ASCII.GetString(encodedDataAsBytes);
                string username = value.Substring(0, value.IndexOf(':'));
                string password = value.Substring(value.IndexOf(':') + 1);

                if (validator.Validate(username, password))
                {
                    return input;
                }
            }

            var unauthorizedResponse = new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
            unauthorizedResponse.Headers.WwwAuthenticate.Add(new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "realm=\"My Server\""));
            unauthorizedResponse.Content = new StringContent("401, please authenticate");

            // Get the client to prompt for credentials
            throw new HttpResponseException(unauthorizedResponse);
        }
    }
}