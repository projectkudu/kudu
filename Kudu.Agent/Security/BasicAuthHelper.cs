using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Agent.Security
{
    public class BasicAuthHelper : ControllerBase
    {

        private readonly RequestDelegate _requestDelegate;

        public BasicAuthHelper(RequestDelegate requestDelegate)
        {
            _requestDelegate = requestDelegate;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                var authHeader = AuthenticationHeaderValue.Parse(context.Request.Headers["Authorization"]);
                var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
                var username = credentials[0];
                var password = credentials[1];

                authenticateUser(username, password);
                
            }
            catch
            {
                throw new UnauthorizedAccessException();
            }

            await _requestDelegate(context);
        }

        public IActionResult authenticateUser(string username, string password)
        {
            if (String.Equals(username, "test") && String.Equals(password, "test2"))
            {
                
                return Ok();
            }
            else
            {
                return BadRequest(new { message = "Username or password is incorrect."});
            }
        }
    }
}
