using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    // This is the site builder used for OneDeploy scenarios
    public class OneDeployBuilder : BasicBuilder
    {
        private DeploymentInfoBase _deploymentInfo;
        private string _repositoryPath;

        public OneDeployBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath, DeploymentInfoBase deploymentInfo)
            : base(environment, settings, propertyProvider, repositoryPath, projectPath)
        {
            _deploymentInfo = deploymentInfo;
            _repositoryPath = repositoryPath;
        }

        public override string ProjectType
        {
            get { return Constants.OneDeploy; }
        }

        public override Task Build(DeploymentContext context)
        {
            context.Logger.Log($"Running build. Project type: {ProjectType}");

            // Start by copying the manifest as-is so that 
            // manifest based deployments (Example: ZipDeploy) are unaffected
            context.Logger.Log($"Copying the manifest");
            FileSystemHelpers.CopyFile(context.PreviousManifestFilePath, context.NextManifestFilePath);

            // If we want to clean up the target directory before copying
            // the new files, use kudusync so that only unnecessary files are 
            // deleted. This has two benefits:
            // 1. This is faster than deleting the target directory before copying the source dir.
            // 2. Minimizes chances of failure in deleting a directory due to open handles.
            //    This is especially useful when a target directory is present in the source and
            //    need not be deleted.
            if (_deploymentInfo.CleanupTargetDirectory)
            {
                context.Logger.Log($"Clean deploying to {context.OutputPath}");

                // We do not want to use the manifest for OneDeploy. Use an empty manifest file.
                // This way we don't interfere with manifest based deployments.
                string tempManifestPath = null;
                try
                {
                    tempManifestPath = Path.GetTempFileName();
                    context.PreviousManifestFilePath = context.NextManifestFilePath = tempManifestPath;
                    base.Build(context);
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(tempManifestPath))
                    {
                        FileSystemHelpers.DeleteFileSafe(tempManifestPath);
                    }
                }
            }
            else
            {
                context.Logger.Log($"Incrementally deploying to {context.OutputPath}");
                FileSystemHelpers.CopyDirectoryRecursive(_repositoryPath, context.OutputPath);
            }

            context.Logger.Log($"Build completed succesfully.");

            return Task.CompletedTask;
        }
    }
}
