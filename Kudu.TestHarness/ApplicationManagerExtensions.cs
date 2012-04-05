using System;

namespace Kudu.TestHarness
{
    public static class ApplicationManagerExtensions
    {
        public static TimeSpan GitDeploy(this ApplicationManager appManager, string localRepoPath, string localBranchName = "master", string remoteBranchName = "master")
        {
            return Git.GitDeploy(appManager.DeploymentManager, appManager.ServiceUrl, localRepoPath, appManager.GitUrl, localBranchName, remoteBranchName);
        }
    }
}
