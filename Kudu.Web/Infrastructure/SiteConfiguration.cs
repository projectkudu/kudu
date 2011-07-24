using System.Web;
using System.Web.Script.Serialization;
using Kudu.Web.Models;

namespace Kudu.Web.Infrastructure {
    public class SiteConfiguration : ISiteConfiguration {
        public SiteConfiguration(HttpContextBase httpContext) {
            var serializer = new JavaScriptSerializer();
            var request = serializer.Deserialize<HubRequest>(httpContext.Request["data"]);

            using (var db = new KuduContext()) {
                var application = db.Applications.Find(request.State.AppId);
                ServiceUrl = application.ServiceUrl;
                SiteUrl = application.SiteUrl;
                Name = application.Name;
            }
        }

        public string Name {
            get;
            private set;
        }

        public string ServiceUrl {
            get;
            private set;
        }

        public string SiteUrl {
            get;
            private set;
        }

        private class HubRequest {
            public ClientState State { get; set; }
        }

        private class ClientState {
            public int AppId { get; set; }
        }
    }
}