using System;
using System.IO;
using Kudu.Core.Infrastructure;
using Mercurial;

namespace Kudu.Core.SourceControl.Hg {
    public class HgServerManager {
        private readonly Lazy<HgServer> _server;
        private readonly IEnvironment _environment;

        private const string HgConfigurationFile = "hgweb.config";
        private const string HgConfiguration = @"[web]
push_ssl = false
allow_push = *

[paths]
{0} = {1}
";
        public HgServer Server {
            get {
                return _server.Value;
            }
        }

        public HgServerManager(IEnvironment environment) {
            _environment = environment;
            _server = new Lazy<HgServer>(() => GetServer(environment));
        }

        private static HgServer GetServer(IEnvironment environment) {
            string configFile = EnsureConfiguration(environment);

            var hgExe = new Executable(Client.ClientPath, environment.RepositoryPath);

            return new HgServer(hgExe, environment.AppName, configFile);
        }

        private static string EnsureConfiguration(IEnvironment environment) {
            string configFile = Path.Combine(environment.ApplicationRootPath, HgConfigurationFile);
            string configFileContents = String.Format(HgConfiguration, environment.AppName, environment.RepositoryPath);

            File.WriteAllText(configFile, configFileContents);
            return configFile;
        }
    }
}
