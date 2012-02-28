using System;

namespace Kudu.TestHarness
{
    public static class ApplicationManagerExtensions
    {
        private static readonly TimeSpan _defaultTimeOut = TimeSpan.FromMinutes(5);

        public static GitDeploymentResult GitDeploy(this ApplicationManager appManager, string localRepoPath, string localBranchName = "master", string remoteBranchName = "master")
        {
            return Git.GitDeploy(appManager.DeploymentManager, appManager.ServiceUrl, localRepoPath, appManager.GitUrl, localBranchName, remoteBranchName, _defaultTimeOut);
        }        
    }
}
