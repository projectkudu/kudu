using System;

namespace Kudu.TestHarness
{
    public static class ApplicationManagerExtensions
    {
        public static GitDeploymentResult GitDeploy(this ApplicationManager appManager, string localRepoPath, string localBranchName = "master", string remoteBranchName = "master")
        {
            GitDeploymentResult result = Git.GitDeploy(appManager.DeploymentManager, appManager.ServiceUrl, localRepoPath, appManager.GitUrl, localBranchName, remoteBranchName);

            string traceFile = String.Format("git-{0:MM-dd-H-mm-ss}.txt", DateTime.Now);

            appManager.Save(traceFile, result.GitTrace);

            return result;
        }
    }
}
