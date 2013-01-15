using System;
using Kudu.Core.Infrastructure;

namespace Kudu.TestHarness
{
    public static class ApplicationManagerExtensions
    {
        public static GitDeploymentResult GitDeploy(this ApplicationManager appManager, string localRepoPath, string localBranchName = "master", string remoteBranchName = "master", int retries = 3)
        {
            return OperationManager.Attempt(() =>
            {
                GitDeploymentResult result = Git.GitDeploy(appManager.DeploymentManager, appManager.ServiceUrl, localRepoPath, appManager.GitUrl, localBranchName, remoteBranchName);

                string traceFile = String.Format("git-push-{0:MM-dd-H-mm-ss}.txt", DateTime.Now);

                appManager.Save(traceFile, result.GitTrace);

                return result;
            }, retries);
        }
    }
}
