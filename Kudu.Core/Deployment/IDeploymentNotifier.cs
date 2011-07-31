namespace Kudu.Core.Deployment {
    public interface IDeploymentNotifier {
        void NotifyStatus(DeployResult deployResult);
    }
}
