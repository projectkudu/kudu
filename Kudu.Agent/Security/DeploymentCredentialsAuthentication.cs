//------------------------------------------------------------------------------
// <copyright file="DeploymentCredentialsAuthentication.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kudu.Agent.Security
{
    public class DeploymentCredentialsAuthentication : AuthenticationHandler<DeploymentCredentialsAuthenticationOptions>
    {
        public DeploymentCredentialsAuthentication(
            IOptionsMonitor<DeploymentCredentialsAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock) :
            base(options, logger, encoder, clock)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string authorizationHeader = Request.Headers["Authorization"];
            AuthenticationHeaderValue authHeader = AuthenticationHeaderValue.Parse(authorizationHeader);
            byte[] credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            string[] credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            string username = credentials[0];
            string password = credentials[1];

            // create a ClaimsPrincipal from your header
            Claim[] claims = new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, username)
            };

            ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            AuthenticationTicket ticket = new AuthenticationTicket(claimsPrincipal,
                new AuthenticationProperties()
                {
                    IsPersistent = false
                },
                Scheme.Name
            );

            return AuthenticateResult.Success(ticket);
        }
    }
}
