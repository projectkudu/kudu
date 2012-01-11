using System;
using System.Threading;
using Kudu.Core.Deployment;
using Xunit;

namespace Kudu.FunctionalTests.Infrastructure
{
    public static class ApplicationManagerExtensions
    {
        private static readonly TimeSpan _defaultTimeOut = TimeSpan.FromMinutes(2);

        public static void GitDeploy(this ApplicationManager appManager, string repositoryName)
        {
            GitDeploy(appManager, repositoryName, _defaultTimeOut);
        }

        public static void GitDeploy(this ApplicationManager appManager, string repositoryName, TimeSpan waitTimeout)
        {
            var deployEvent = new ManualResetEvent(false);

            try
            {
                // Create deployment manager and wait for the deployment to finish                
                var deploymentManager = appManager.DeploymentManager;
                deploymentManager.StatusChanged += status =>
                {
                    if (status.Status == DeployStatus.Success || status.Status == DeployStatus.Failed)
                    {
                        deployEvent.Set();
                    }
                };

                // Start listenting for events
                deploymentManager.Start();

                // Push the repository
                Git.Push(repositoryName, appManager.GitUrl);

                // Stop listenting
                if (!deployEvent.WaitOne(waitTimeout))
                {
                    Assert.True(false, "Waiting for deployment timeout out!");
                }

                deploymentManager.Stop();
            }
            catch
            {
                deployEvent.Set();
                throw;
            }
        }

    }
}
