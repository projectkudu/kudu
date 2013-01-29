using System;
using System.Web;
using System.Web.Mvc;

namespace Kudu.Services.Web
{
    public class MvcApplication : HttpApplication
    {
        private readonly static DateTime _startDateTime = DateTime.UtcNow;

        public static TimeSpan UpTime
        {
            get { return DateTime.UtcNow - _startDateTime; }
        }

        protected void Application_Start(object sender, EventArgs e)
        {
            AreaRegistration.RegisterAllAreas();
        }
    }
}