using System;
using System.Web;
using System.Web.Mvc;

namespace Kudu.Services.Web
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            AreaRegistration.RegisterAllAreas();
        }
    }
}