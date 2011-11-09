using System.Collections.Generic;
using System.Web;
using System.Web.Script.Serialization;

namespace Kudu.SignalR.Infrastructure
{
    public static class ClientStateResolver
    {
        public static IDictionary<string, object> GetState(HttpContextBase httpContext)
        {
            var serializer = new JavaScriptSerializer();
            var request = serializer.Deserialize<HubRequest>(httpContext.Request["data"]);
            return request.State;
        }

        private class HubRequest
        {
            public IDictionary<string, object> State { get; set; }
        }
    }
}
