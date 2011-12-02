using System;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class BasicBuilder : ISiteBuilder
    {
        private readonly string _sourcePath;

        public BasicBuilder(string sourcePath)
        {
            _sourcePath = sourcePath;
        }

        public Task Build(string outputPath, ILogger logger)
        {
            var tcs = new TaskCompletionSource<object>();

            var innerLogger = logger.Log("Copying files to {0}.", outputPath);

            try
            {
                FileSystemHelpers.SmartCopy(_sourcePath, outputPath);

                innerLogger.Log("Done.");
            }
            catch (Exception ex)
            {
                innerLogger.Log("Copying files failed.");
                innerLogger.Log(ex);
                tcs.SetException(ex);
            }

            tcs.SetResult(null);

            return tcs.Task;
        }
    }
}
