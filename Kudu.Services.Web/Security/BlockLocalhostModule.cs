using System.Web;

namespace Kudu.Services.Web.Security
{
    public class BlockLocalhostModule : IHttpModule
    {
        public void Dispose()
        {
            
        }

        public void Init(HttpApplication app)
        {
            app.BeginRequest += (sender, e) =>
            {
                if (app.Context.Request.IsLocal)
                {
                    app.Response.StatusCode = 403;
                    app.CompleteRequest();
                }
            };
        }
    }
}