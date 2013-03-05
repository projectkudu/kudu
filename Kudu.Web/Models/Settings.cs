using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace Kudu.Web.Models
{
    internal class Settings : ISettings
    {
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "By design")]
        public NameValueCollection AppSettings
        {
            get;
            set;
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "By design")]
        public NameValueCollection ConnectionStrings
        {
            get;
            set;
        }

        public NameValueCollection KuduSettings
        {
            get;
            set;
        }
    }
}