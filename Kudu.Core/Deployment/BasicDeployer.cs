using System;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment {
    public class BasicDeployer : IDeployer {
        private readonly string _sourcePath;
        
        public BasicDeployer(string sourcePath) {
            _sourcePath = sourcePath;
        }

        public Task Deploy(string targetPath, ILogger logger) {
            var tcs = new TaskCompletionSource<object>();

            try {
                DeploymentHelpers.SmartCopy(_sourcePath, targetPath);
            }
            catch(Exception ex) {
                tcs.SetException(ex);
            }

            tcs.SetResult(null);

            return tcs.Task;
        }
    }
}
