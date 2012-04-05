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
        private static int _errorCallbackInitialized;

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
