using System;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests.Infrastructure
{
    public static class ApplicationManagerExtensions
    {
        public static void AssertGitDeploy(this ApplicationManager appManager, string localRepoPath, string localBranchName = "master", string remoteBranchName = "master")
        {
            appManager.GitDeploy(localRepoPath, localBranchName, remoteBranchName);
        }
    }
}
