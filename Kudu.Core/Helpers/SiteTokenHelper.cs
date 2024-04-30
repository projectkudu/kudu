//------------------------------------------------------------------------------
// <copyright file="SiteTokenHelper.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Kudu.Core.Settings;
using Microsoft.IdentityModel.Tokens;

namespace Kudu.Core.Helpers
{
    /// <summary>
    /// Helper to issue x-ms-site-token for site and app service communication.
    /// </summary>
    public static class SiteTokenHelper
    {
        public const string SiteTokenHeader = "x-ms-site-token";
        public const string AppServiceAudience = "https://appservice.core.azurewebsites.net";
        public static readonly TimeSpan SiteTokenValidity = TimeSpan.FromHours(1);

        // this represents Admin Site Name in raw form without punycode
        private static readonly Lazy<string> SiteTokenIssuer = new Lazy<string>(() =>
        {
            var webSiteName = System.Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            return $"https://{webSiteName}.scm.azurewebsites.net";
        });

        public static readonly Lazy<string> FunctionSiteAudience = new Lazy<string>(() =>
        {
            var webSiteName = System.Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            return $"https://{webSiteName}.azurewebsites.net";
        });

        public static string IssueToken(TimeSpan? validity = null, string audience = null)
        {
            var utcNow = DateTime.UtcNow;
            var expires = utcNow.Add(validity.GetValueOrDefault(SiteTokenValidity));

            return new JwtSecurityTokenHandler().CreateEncodedJwt(
                issuer: SiteTokenIssuer.Value,
                audience: !string.IsNullOrEmpty(audience) ? audience : AppServiceAudience,
                subject: null,
                notBefore: utcNow,
                expires: expires,
                issuedAt: utcNow,
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(GetWebSiteAuthEncryptionKey()), SecurityAlgorithms.HmacSha256Signature));
        }

        // SiteTokenIssuingMode
        // 0: (default) add both x-ms-site-restricted-token and x-ms-site-token
        // 1: add x-ms-site-restricted-token only
        // 2: add x-ms-site-token only
        public static bool ShouldAddSiteRestrictedToken() => 2 != ScmHostingConfigurations.SiteTokenIssuingMode;
        public static bool ShouldAddSiteToken() => 1 != ScmHostingConfigurations.SiteTokenIssuingMode;

        private static byte[] GetWebSiteAuthEncryptionKey()
        {
            var hexOrBase64 = System.Environment.GetEnvironmentVariable(Constants.SiteAuthEncryptionKey);
            if (string.IsNullOrEmpty(hexOrBase64))
            {
                throw new InvalidOperationException($"No {Constants.SiteAuthEncryptionKey} defined in the environment");
            }

            // only support 32 bytes (256 bits) key length
            if (hexOrBase64.Length == 64)
            {
                return Enumerable.Range(0, hexOrBase64.Length)
                                 .Where(x => x % 2 == 0)
                                 .Select(x => Convert.ToByte(hexOrBase64.Substring(x, 2), 16))
                                 .ToArray();
            }

            return Convert.FromBase64String(hexOrBase64);
        }
    }
}
