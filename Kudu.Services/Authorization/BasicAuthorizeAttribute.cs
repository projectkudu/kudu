#region License
//  
// Parts of this code come from http://bonobogitserver.codeplex.com
// License: Microsoft Public License (Ms-PL)
#endregion

using System;
using System.Text;
using System.Web.Mvc;

namespace Kudu.Services.Authorization {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class BasicAuthorizeAttribute : AuthorizeAttribute {
        private IUserValidator _userValidator;

        public BasicAuthorizeAttribute() {
            _userValidator = DependencyResolver.Current.GetService<IUserValidator>();
        }

        public override void OnAuthorization(AuthorizationContext filterContext) {
            var authorizationHeader = filterContext.HttpContext.Request.Headers["Authorization"];

            if (!String.IsNullOrEmpty(authorizationHeader)) {
                byte[] encodedDataAsBytes = Convert.FromBase64String(authorizationHeader.Replace("Basic ", ""));
                string value = Encoding.ASCII.GetString(encodedDataAsBytes);
                string username = value.Substring(0, value.IndexOf(':'));
                string password = value.Substring(value.IndexOf(':') + 1);

                if (_userValidator.Validate(username, password)) {
                    return;
                }
            }

            // Get the client to prompt for credentials

            filterContext.HttpContext.Response.Clear();
            filterContext.HttpContext.Response.StatusCode = 401;
            filterContext.HttpContext.Response.StatusDescription = "Unauthorized";
            filterContext.HttpContext.Response.AddHeader("WWW-Authenticate", "Basic realm=\"My Server\"");
            filterContext.HttpContext.Response.Write("401, please authenticate");
            filterContext.HttpContext.Response.End();
        }
    }
}
