using System.Collections.Specialized;

namespace Kudu.Web.Models
{
    public interface ISettings
    {
        NameValueCollection KuduSettings { get; }
        NameValueCollection AppSettings { get; }
        NameValueCollection ConnectionStrings { get; }
    }
}
