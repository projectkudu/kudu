using System.Collections;
using System.IO;
using System.Web.Hosting;
using Elmah;

namespace Kudu.Services.Web.Elmah
{
    public class KuduErrorLog : XmlFileErrorLog
    {
        internal const string ElmahErrorLogPath = "elmah";

        public KuduErrorLog(IDictionary config)
            : base(GetPath())
        {
        }

        private static string GetPath()
        {
            string path = HostingEnvironment.MapPath(Constants.MappedLiveSite);
            return Path.Combine(path, ElmahErrorLogPath);
        }
    }
}