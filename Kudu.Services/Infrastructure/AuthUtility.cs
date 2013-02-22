using System;
using System.Net.Http;
using System.Text;
using System.Web;

namespace Kudu.Services.Infrastructure
{
    public static class AuthUtility
    {
        // HTTP 1.1 Authorization header
        public const string HttpAuthorizationHeader = "Authorization";

        // HTTP 1.1 Basic Challenge Scheme Name
        public const string HttpBasicSchemeName = "Basic";

        // HTTP 1.1 Credential username and password separator
        public const char HttpCredentialSeparator = ':';

        public static bool TryExtractBasicAuthUser(HttpRequestMessage request, out string username)
        {
            username = null;
            if (request.Headers.Authorization != null &&
                String.Equals(request.Headers.Authorization.Scheme, HttpBasicSchemeName, StringComparison.OrdinalIgnoreCase))
            {
                return TryParseBasicAuthUserFromHeaderParameter(request.Headers.Authorization.Parameter, out username);
            }

            return false;
        }

        public static bool TryExtractBasicAuthUser(HttpRequestBase request, out string username)
        {
            string authorizationHeader = request.Headers[HttpAuthorizationHeader];

            return TryExtractBasicAuthUserFromHeader(authorizationHeader, out username);
        }

        public static bool TryExtractBasicAuthUserFromHeader(string authorizationHeader, out string username)
        {
            username = null;
            if (String.IsNullOrEmpty(authorizationHeader))
            {
                return false;
            }

            string verifiedAuthorizationHeader = authorizationHeader.Trim();
            if (verifiedAuthorizationHeader.IndexOf(HttpBasicSchemeName, StringComparison.Ordinal) != 0)
            {
                return false;
            }

            // Get the credential payload
            verifiedAuthorizationHeader = verifiedAuthorizationHeader.Substring(HttpBasicSchemeName.Length, verifiedAuthorizationHeader.Length - HttpBasicSchemeName.Length).Trim();

            return TryParseBasicAuthUserFromHeaderParameter(verifiedAuthorizationHeader, out username);
        }

        public static bool TryParseBasicAuthUserFromHeaderParameter(string verifiedAuthorizationHeader, out string username)
        {
            username = null;
            // Decode the base 64 encoded credential payload 
            byte[] credentialBase64DecodedArray = Convert.FromBase64String(verifiedAuthorizationHeader);

            string decodedAuthorizationHeader = Encoding.UTF8.GetString(credentialBase64DecodedArray, 0, credentialBase64DecodedArray.Length);

            // get the username, password, and realm 
            int separatorPosition = decodedAuthorizationHeader.IndexOf(HttpCredentialSeparator);

            if (separatorPosition <= 0)
            {
                return false;
            }

            username = decodedAuthorizationHeader.Substring(0, separatorPosition).Trim();

            if (String.IsNullOrEmpty(username))
            {
                return false;
            }

            return true;
        }
    }
}
