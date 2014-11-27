using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Kudu.SiteManagement.Configuration;

namespace Kudu.SiteManagement.Context
{
    public interface IKuduContext
    {
        IPathResolver Paths { get; }
        IKuduConfiguration Configuration { get; }
        
        Version IISVersion { get; }
        IEnumerable<string> IPAddresses { get; }
    }

    public class KuduContext : IKuduContext
    {
        public IPathResolver Paths { get; private set; }
        public IKuduConfiguration Configuration { get; private set; }
        public Version IISVersion { get { return HttpRuntime.IISVersion; } }
        public IEnumerable<string> IPAddresses { get { return GetAddresses(); } }

        public KuduContext(IKuduConfiguration configuration, IPathResolver paths)
        {
            Configuration = configuration;
            Paths = paths;
        }
        private static IEnumerable<string> GetAddresses()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return (from ip in host.AddressList where ip.AddressFamily == AddressFamily.InterNetwork select ip.ToString()).ToList();
        }        
    }
}
