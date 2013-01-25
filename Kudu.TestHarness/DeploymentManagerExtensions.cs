using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Kudu.TestHarness
{
    public static class DeploymentManagerExtensions
    {
        public static XDocument GetServerProfile(this ApplicationManager appManager, string applicationName)
        {
            var zippedLogsPath = Path.Combine(PathHelper.TestResultsPath, applicationName + ".zip");
            var unzippedLogsPath = Path.Combine(PathHelper.TestResultsPath, applicationName);
            var profileLogPath = Path.Combine(unzippedLogsPath, "profiles", "profile.xml");

            return KuduUtils.GetServerProfile(appManager.ServiceUrl, zippedLogsPath, applicationName);
        }

        private static void WaitForRepositorySite(ApplicationManager appManager)
        {
            HttpUtils.WaitForSite(appManager.ServiceUrl, appManager.DeploymentManager.Credentials);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            TestTracer.Trace("OnUnobservedTaskException - {0}", e.Exception.GetBaseException());
            e.SetObserved();
        }
    }
}
