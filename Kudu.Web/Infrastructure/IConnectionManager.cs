using SignalR.Client;

namespace Kudu.Web.Infrastructure {
    public interface IConnectionManager {
        Connection CreateConnection(string applicationName);
        void RemoveConnection(string applicationName);
    }
}
