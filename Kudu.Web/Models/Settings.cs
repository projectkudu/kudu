using System.Collections.Specialized;

namespace Kudu.Web.Models
{
    public class Settings : ISettings
    {
        public NameValueCollection AppSettings
        {
            get;
            set;
        }

        public NameValueCollection ConnectionStrings
        {
            get;
            set;
        }
    }
}