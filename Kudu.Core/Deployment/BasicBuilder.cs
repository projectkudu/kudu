using System;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment {
    public class BasicBuilder : ISiteBuilder {
        private readonly string _sourcePath;
        
        public BasicBuilder(string sourcePath) {
            _sourcePath = sourcePath;
        }

        public Task Build(string outputPath, ILogger logger) {
            var tcs = new TaskCompletionSource<object>();

            try {
                logger.Log("Copying files to {0}", outputPath);

                FileSystemHelpers.SmartCopy(_sourcePath, outputPath);

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
