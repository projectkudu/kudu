using System.Collections.Specialized;

namespace Kudu.Web.Models
{
    public interface ISettings
    {
        NameValueCollection AppSettings { get; }
        NameValueCollection ConnectionStrings { get; }
    }
}
