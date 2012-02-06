using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Xunit;
using System.Net.Http;
using System.Net;
using System.Configuration;

namespace Kudu.FunctionalTests.Infrastructure
{
    public static class ApplicationManagerExtensions
    {
        private static readonly TimeSpan _defaultTimeOut = TimeSpan.FromMinutes(4);
        private static bool _errorCallbackInitialized;

        public static void GitDeploy(this ApplicationManager appManager, string repositoryName)
        {
            GitDeploy(appManager, repositoryName, _defaultTimeOut);
        }

        public static void GitDeploy(this ApplicationManager appManager, string repositoryName, TimeSpan waitTimeout)
        {
            appManager.DeploymentManager.WaitForDeployment(() =>
            {

                WaitForRepositorySite(appManager);
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

        private static void WaitForRepositorySite(ApplicationManager appManager, int retries = 3, int delayBeforeRetry = 250)
        {
            HttpClientHandler handler = new HttpClientHandler();
            if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["antares.basedomain"]))
            {
                string userName = ConfigurationManager.AppSettings["antares.username"];
                handler.UseDefaultCredentials = false;
                handler.Credentials = new NetworkCredential(userName, userName);
            }

            HttpClient client = new HttpClient(handler);

            while (retries > 0)
            {
                try
                {
                    var response = client.GetAsync(appManager.ServiceUrl).Result;
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        break;
                    }
                }
                catch
                {
                    if (retries == 0)
                    {
                        throw;
                    }
                }
                retries--;
                Thread.Sleep(delayBeforeRetry);
            }
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine(e.Exception.GetBaseException().ToString());
            e.SetObserved();
        }
    }
}
