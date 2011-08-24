using System.Web;
using System.Web.Script.Serialization;

namespace Kudu.Client.Infrastructure {
    public static class ApplicationNameResolver {
        public static string ResolveName(HttpContextBase httpContext) {
            var serializer = new JavaScriptSerializer();
            var request = serializer.Deserialize<HubRequest>(httpContext.Request["data"]);
            return request.State.AppName;
        }

        private class HubRequest {
            public ClientState State { get; set; }
        }

        private class ClientState {
            public string AppName { get; set; }
        }
    }
}
