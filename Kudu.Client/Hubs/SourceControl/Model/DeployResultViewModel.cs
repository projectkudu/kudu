using Kudu.Core.Deployment;

namespace Kudu.Client.Model {
    public class DeployResultViewModel {
        public DeployResultViewModel(DeployResult result) {
            Id = result.Id;
            Status = result.Status.ToString();
            StatusText = result.StatusText;
        }

        public string Id { get; set; }
        public string StatusText { get; set; }
        public string Status { get; set; }
    }
}