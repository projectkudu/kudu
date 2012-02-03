using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Xunit;

namespace Kudu.FunctionalTests.Infrastructure
{
    public static class ApplicationManagerExtensions
    {
        private static readonly TimeSpan _defaultTimeOut = TimeSpan.FromMinutes(3);
        private static bool _errorCallbackInitialized;

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

            if (!_errorCallbackInitialized)
            {
                _errorCallbackInitialized = true;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            }

            try
            {
                // Create deployment manager and wait for the deployment to finish
                deploymentManager.StatusChanged += status =>
                {
                    if (status.Complete)
                    {
                        deployEvent.Set();
                    }
                };

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
                // Stop listenting
                deploymentManager.Stop();
            }
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine(e.Exception.GetBaseException().ToString());
            e.SetObserved();
        }
    }
}
