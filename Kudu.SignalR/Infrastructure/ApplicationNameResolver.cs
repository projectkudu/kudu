using System.Web;
using System.Web.Script.Serialization;

namespace Kudu.SignalR.Infrastructure {
    public static class ApplicationNameResolver {
        public static string ResolveName(HttpContextBase httpContext) {
            var serializer = new JavaScriptSerializer();
            var request = serializer.Deserialize<HubRequest>(httpContext.Request["data"]);
            return request.State.ApplicationName;
        }

        private class HubRequest {
            public ClientState State { get; set; }
        }

        private class ClientState {
            public string ApplicationName { get; set; }
        }
    }
}
