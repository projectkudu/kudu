using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Kudu.ContainerServices.Agent.Util;
using Microsoft.Extensions.Primitives;

namespace Kudu.ContainerServices.Agent.Security
{   
    public class BasicAuthHelper : ControllerBase
    {

        private readonly RequestDelegate _requestDelegate;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
            int dwLogonType, int dwLogonProvider, out SafeAccessTokenHandle phToken);

        public BasicAuthHelper(RequestDelegate requestDelegate)
        {
            _requestDelegate = requestDelegate;
        }

        public async Task Invoke(HttpContext context)
        {         
            if (!OSDetector.IsOnWindows())
            {
                // PrincipalContext method for validating credentials only works on Windows
                throw new NotImplementedException();
            }

            StringValues hostname;
            if (context.Request.Headers.TryGetValue("WAS-DEFAULT-HOSTNAME", out hostname)) {
                // Scm site name needed for some webjob operations
                System.Environment.SetEnvironmentVariable("HTTP_HOST", hostname);
            }
            else
            {
                throw new MissingFieldException("Header field WAS-DEFAULT-HOSTNAME is missing.");
            }

            var authHeader = AuthenticationHeaderValue.Parse(context.Request.Headers["Authorization"]);
            if (authHeader == null)
            {
                // Should never be reached. Web worker should always add authentication
                context.Response.StatusCode = ((int) HttpStatusCode.Unauthorized);
                await context.Response.WriteAsync("No authentication header attached.");
            }
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            var username = credentials[0];
            var password = credentials[1];

            const int LOGON32_PROVIDER_DEFAULT = 0;
            //This parameter causes LogonUser to create a primary token.   
            const int LOGON32_LOGON_INTERACTIVE = 2;

            // Call LogonUser to obtain a handle to an access token.   
            SafeAccessTokenHandle safeAccessTokenHandle;
            bool success = LogonUser(username, "", password,
                LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT,
                out safeAccessTokenHandle);
            if (success)
            {
                await _requestDelegate(context);
            }
            else
            {
                context.Response.StatusCode = 401; //UnAuthorized
                await context.Response.WriteAsync("Username or password is incorrect.");
            }
        }
    }
}
