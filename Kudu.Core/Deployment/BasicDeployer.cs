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
                logger.Log("Copying files to {0}", targetPath);

                DeploymentHelpers.SmartCopy(_sourcePath, targetPath);

                logger.Log("Success.");
            }
            catch(Exception ex) {
                logger.Log("Copying files failed.");
                logger.Log(ex.Message);
                tcs.SetException(ex);
            }

            tcs.SetResult(null);

            return tcs.Task;
        }
    }
}
