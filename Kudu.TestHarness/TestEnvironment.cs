using Kudu.Core;

namespace Kudu.TestHarness
{
    public class TestEnvironment : IEnvironment
    {
        public string RootPath
        {
            get;
            set;
        }

        public string SiteRootPath
        {
            get;
            set;
        }

        public string RepositoryPath
        {
            get;
            set;
        }

        public string WebRootPath
        {
            get;
            set;
        }

        public string DeploymentsPath
        {
            get;
            set;
        }

        public string DeploymentToolsPath
        {
            get;
            set;
        }

        public string DiagnosticsPath
        {
            get;
            set;
        }

        public string SSHKeyPath
        {
            get;
            set;
        }

        public string TempPath
        {
            get;
            set;
        }

        public string ScriptPath
        {
            get;
            set;
        }

        public string NodeModulesPath
        {
            get;
            set;
        }

        public string LogFilesPath
        {
            get;
            set;
        }

        public string TracePath
        {
            get;
            set;
        }

        public string AnalyticsPath
        {
            get;
            set;
        }

        public string DeploymentTracePath
        {
            get;
            set;
        }

        public string DataPath
        {
            get;
            set;
        }

        public string JobsDataPath
        {
            get;
            set;
        }

        public string JobsBinariesPath
        {
            get;
            set;
        }
    }
}
