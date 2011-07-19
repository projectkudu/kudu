#region License
//  
// Parts of this code come from http://bonobogitserver.codeplex.com
// License: Microsoft Public License (Ms-PL)
#endregion

using System;
using System.Text;
using System.Web.Mvc;
using Ninject;

namespace Kudu.Services.Authorization {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class BasicAuthorizeAttribute : AuthorizeAttribute {

        // This is not as clean as ctor injection, but that doesn't work in attributes
        [Inject]
        public IUserValidator UserValidator { get; set; }

        public override void OnAuthorization(AuthorizationContext filterContext) {
            var authorizationHeader = filterContext.HttpContext.Request.Headers["Authorization"];

            if (!String.IsNullOrEmpty(authorizationHeader)) {
                byte[] encodedDataAsBytes = Convert.FromBase64String(authorizationHeader.Replace("Basic ", ""));
                string value = Encoding.ASCII.GetString(encodedDataAsBytes);
                string username = value.Substring(0, value.IndexOf(':'));
                string password = value.Substring(value.IndexOf(':') + 1);

                if (UserValidator.Validate(username, password)) {
                    return;
                }
            }

            // Get the client to prompt for credentials

            filterContext.Result = new HttpUnauthorizedResult();

            filterContext.HttpContext.Response.Clear();
            filterContext.HttpContext.Response.StatusCode = 401;
            filterContext.HttpContext.Response.StatusDescription = "Unauthorized";
            filterContext.HttpContext.Response.AddHeader("WWW-Authenticate", "Basic realm=\"My Server\"");
            filterContext.HttpContext.Response.Write("401, please authenticate");
            filterContext.HttpContext.Response.End();
        }
    }
}
