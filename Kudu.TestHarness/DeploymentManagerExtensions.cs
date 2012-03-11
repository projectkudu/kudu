using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Kudu.Client.Deployment;
using Kudu.Core.Deployment;

namespace Kudu.TestHarness
{
    public static class DeploymentManagerExtensions
    {
        private static readonly TimeSpan _defaultTimeOut = TimeSpan.FromMinutes(5);
        private static int _errorCallbackInitialized;

        public static Tuple<TimeSpan, bool> WaitForDeployment(this RemoteDeploymentManager deploymentManager, Action action)
        {
            return WaitForDeployment(deploymentManager, action, _defaultTimeOut);
        }

        public static Tuple<TimeSpan, bool> WaitForDeployment(this RemoteDeploymentManager deploymentManager, Action action, TimeSpan waitTimeout)
        {
            Stopwatch sw = null;
            bool timedOut = false;
            var deployEvent = new ManualResetEvent(false);

            Action<DeployResult> handler = null;

            handler = status =>
            {
                if (status.Complete)
                {
                    deployEvent.Set();

                    // Stop measuring elapsed time
                    sw.Stop();
                }
            };

            if (Interlocked.Exchange(ref _errorCallbackInitialized, 1) == 0)
            {
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            }

            try
            {
                // Start measuring elapsed time
                sw = Stopwatch.StartNew();  

                // Do something
                action();

                // Create deployment manager and wait for the deployment to finish
                deploymentManager.StatusChanged += handler;

                timedOut = deployEvent.WaitOne(waitTimeout);                
            }
            catch
            {
                deployEvent.Set();
                throw;
            }
            finally
            {
                deploymentManager.StatusChanged -= handler;
            }

            return Tuple.Create(sw.Elapsed, timedOut);
        }

        public static XDocument GetServerProfile(this ApplicationManager appManager, string applicationName)
        {            
            var zippedLogsPath = Path.Combine(PathHelper.TestResultsPath, applicationName + ".zip");
            var unzippedLogsPath = Path.Combine(PathHelper.TestResultsPath, applicationName);
            var profileLogPath = Path.Combine(unzippedLogsPath, "profiles", "profile.xml");

            return KuduUtils.GetServerProfile(appManager.ServiceUrl, zippedLogsPath, applicationName);            
        }

        private static void WaitForRepositorySite(ApplicationManager appManager)
        {
            HttpUtils.WaitForSite(appManager.ServiceUrl);
        }

        
        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine(e.Exception.GetBaseException().ToString());
            e.SetObserved();
        }
    }
}
