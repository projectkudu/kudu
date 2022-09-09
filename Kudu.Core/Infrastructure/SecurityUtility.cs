#if NETFRAMEWORK
using Microsoft.Azure.Web.DataProtection;
#endif
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Kudu.Core.Infrastructure
{
    public static class SecurityUtility
    {
        private const string DefaultProtectorPurpose = "function-secrets";

        private static string GenerateSecretString()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] data = new byte[40];
                rng.GetBytes(data);
                string secret = Convert.ToBase64String(data);
                // Replace pluses as they are problematic as URL values
                return secret.Replace('+', 'a');
            }
        }

        public static Tuple<string, string>[] GenerateSecretStringsKeyPair(int number)
        {
            var unencryptedToEncryptedKeyPair = new Tuple<string, string>[number];
#if NETFRAMEWORK
            var protector = Microsoft.Azure.Web.DataProtection.DataProtectionProvider.CreateAzureDataProtector().CreateProtector(DefaultProtectorPurpose);
            for (int i = 0; i < number; i++)
            {
                string unencryptedKey = GenerateSecretString();
                unencryptedToEncryptedKeyPair[i] = new Tuple<string, string>(unencryptedKey, protector.Protect(unencryptedKey));
            }
            return unencryptedToEncryptedKeyPair;
#else
            throw new NotImplementedException();
#endif
        }

        public static string DecryptSecretString(string content)
        {
            try
            {
#if NETFRAMEWORK
                var protector = Microsoft.Azure.Web.DataProtection.DataProtectionProvider.CreateAzureDataProtector().CreateProtector(DefaultProtectorPurpose);
                return protector.Unprotect(content);
#else
                throw new NotImplementedException();
#endif
            }
            catch (CryptographicException ex)
            {
                throw new FormatException($"unable to decrypt {content}, the key is either invalid or malformed", ex);
            }
        }

        public static string GenerateFunctionToken()
        {
#if NET6_0_OR_GREATER
            throw new NotImplementedException();
#else
            string siteName = ServerConfiguration.GetApplicationName();
            string issuer = $"https://{siteName}.scm.azurewebsites.net";
            string audience = $"https://{siteName}.azurewebsites.net/azurefunctions";
            return JwtGenerator.GenerateToken(issuer, audience, expires: DateTime.UtcNow.AddMinutes(5));
#endif
        }
    }
}

