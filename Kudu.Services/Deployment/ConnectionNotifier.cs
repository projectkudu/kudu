using Kudu.Core.Deployment;
using SignalR;

namespace Kudu.Services.Deployment {
    public class ConnectionNotifier : IDeploymentNotifier {        
        public void NotifyStatus(DeployResult deployResult) {
            IConnection connection = Connection.GetConnection<DeploymentStatusHandler>();
            connection.Broadcast(deployResult);
        }
    }
}
