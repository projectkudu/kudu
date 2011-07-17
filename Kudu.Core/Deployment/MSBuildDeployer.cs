namespace Kudu.Core.Deployment {
    public class MSBuildDeployer : IDeployer {
        private readonly string _source;
        private readonly string _destination;

        public MSBuildDeployer(string source, string destination) {
            _source = source;
            _destination = destination;
        }

        public void Deploy(string id) {
            // TODO: Implement
        }
    }
}
