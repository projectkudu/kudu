using System;
using System.Collections.Concurrent;
using Kudu.Web.Models;
using SignalR.Client;

namespace Kudu.Web.Infrastructure {
    public class ConnectionManager : IConnectionManager {
        private readonly ConcurrentDictionary<string, Connection> _cache = new ConcurrentDictionary<string, Connection>(StringComparer.OrdinalIgnoreCase);

        public static readonly ConnectionManager _instance = new ConnectionManager();

        private ConnectionManager() {
        }

        public static ConnectionManager Instance {
            get {
                return _instance;
            }
        }

        public void RemoveConnection(string applicationName) {
            Connection connection;
            _cache.TryRemove(applicationName, out connection);
            if (connection != null) {
                connection.Stop();
            }
        }

        public Connection CreateConnection(string applicationName) {
            return _cache.GetOrAdd(applicationName, name => {
                using (var db = new KuduContext()) {
                    var application = db.Applications.Find(name);
                    return new Connection(application.ServiceUrl + "deploy/progress");
                }
            });
        }
    }
}