using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Kudu.Core.Deployment;
using Xunit;

namespace Kudu.FunctionalTests.Infrastructure
{
    public static class ApplicationManagerExtensions
    {
        private static readonly TimeSpan _defaultTimeOut = TimeSpan.FromMinutes(5);
        private static int _errorCallbackInitialized;

        public static void GitDeploy(this ApplicationManager appManager, string repositoryName)
        {
            GitDeploy(appManager, repositoryName, _defaultTimeOut);
        }

        public static void GitDeploy(this ApplicationManager appManager, string repositoryName, TimeSpan waitTimeout)
        {
            appManager.DeploymentManager.WaitForDeployment(() =>
            {
                Git.Push(repositoryName, appManager.GitUrl);
            },
            waitTimeout);
        }

        public static void WaitForDeployment(this RemoteDeploymentManager deploymentManager, Action action)
        {
            WaitForDeployment(deploymentManager, action, _defaultTimeOut);
        }

        public static void WaitForDeployment(this RemoteDeploymentManager deploymentManager, Action action, TimeSpan waitTimeout)
        {
            var deployEvent = new ManualResetEvent(false);

            Action<DeployResult> handler = null;

            handler = status =>
            {
                if (status.Complete)
                {
                    if (Interlocked.Exchange(ref handler, null) != null)
                    {
                        deploymentManager.Stop();
                        deploymentManager.StatusChanged -= handler;
                        deployEvent.Set();
                    }
                }
            };

            if (Interlocked.Exchange(ref _errorCallbackInitialized, 1) == 0)
            {
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            }

            try
            {
                // Create deployment manager and wait for the deployment to finish
                deploymentManager.StatusChanged += handler;

                // Start listenting for events
                deploymentManager.Start();

                // Do something
                action();

                Assert.True(deployEvent.WaitOne(waitTimeout), "Waiting for deployment timeout out!");
            }
            catch
            {
                deployEvent.Set();
                throw;
            }
            finally
            {
                if (Interlocked.Exchange(ref handler, null) != null)
                {
                    deploymentManager.StatusChanged -= handler;
                    // Stop listenting
                    deploymentManager.Stop();
                }
            }
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine(e.Exception.GetBaseException().ToString());
            e.SetObserved();
        }
    }
}
