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
                    if (status.Complete)
                    {
                        deployEvent.Set();
                    }
                };

                // Start listenting for events
                deploymentManager.Start();

                // Push the repository
                Git.Push(repositoryName, appManager.GitUrl);

                Assert.True(deployEvent.WaitOne(waitTimeout), "Waiting for deployment timeout out!");

                // Stop listenting
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
