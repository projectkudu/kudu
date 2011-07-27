using System;
using System.IO;
using System.Threading;
using Kudu.Core.Infrastructure;
using Mercurial;
using System.Diagnostics;

namespace Kudu.Core.SourceControl.Hg {
    public class HgServer : IServer {
        private readonly Lazy<Server> _server;
        private readonly IEnvironment _environment;

        private const string HgConfigurationFile = "hgweb.config";
        private const string HgConfiguration = @"[web]
push_ssl = false
allow_push = *

[paths]
{0} = {1}
";
        public HgServer(IEnvironment environment) {
            _environment = environment;
            _server = new Lazy<Server>(() => GetServer(environment));
        }

        public bool IsRunning {
            get {
                return _server.IsValueCreated && _server.Value.IsRunning;
            }
        }

        public string Url {
            get {
                if (!_server.IsValueCreated) {
                    return null;
                }
                return _server.Value.Url;
            }
        }

        public void Start() {
            _server.Value.Start();
        }

        public void Stop() {
            if (_server.IsValueCreated) {
                _server.Value.Stop();
            }
        }

        private static Server GetServer(IEnvironment environment) {
            string configFile = EnsureConfiguration(environment);
            string pidFilePath = Path.Combine(environment.ApplicationRootPath, "serverpid");

            var hgExe = new Executable(Client.ClientPath, environment.RepositoryPath);

            return new Server(hgExe, environment.AppName, configFile, pidFilePath);
        }

        private static string EnsureConfiguration(IEnvironment environment) {
            string configFile = Path.Combine(environment.ApplicationRootPath, HgConfigurationFile);
            string configFileContents = String.Format(HgConfiguration, environment.AppName, environment.RepositoryPath);

            File.WriteAllText(configFile, configFileContents);
            return configFile;
        }

        private class Server {
            private readonly Executable _hgExe;
            private readonly string _appName;
            private readonly string _configFile;
            private readonly string _pidFile;

            internal Server(Executable hgExe, string appName, string configFile, string pidFile) {
                _hgExe = hgExe;
                _appName = appName;
                _configFile = configFile;
                _pidFile = pidFile;
            }

            private int Port { get; set; }

            public string Url {
                get {
                    if (Port > 0) {
                        return String.Format("http://localhost:{0}/{1}", Port, _appName);
                    }
                    return null;
                }
            }

            public bool IsRunning { get; private set; }

            public void Start() {
                if (IsRunning) {
                    return;
                }

                while (true) {
                    try {
                        // Get a random port
                        Port = GetRandomPort();

                        // Start the server as a daemon
                        _hgExe.Execute(@"serve -d --port {0} --web-conf ""{1}"" --pid-file ""{2}""", Port, _configFile, _pidFile);

                        // Mark the server as running
                        IsRunning = true;
                        break;
                    }
                    catch {
                        // Try again
                        Thread.Sleep(500);
                    }
                }
            }

            public void Stop() {
                if (!IsRunning) {
                    return;
                }

                // Read the pid file and kill the process
                int pid;
                if (Int32.TryParse(File.ReadAllText(_pidFile), out pid)) {
                    try {
                        var process = Process.GetProcessById(pid);
                        process.Kill();
                    }
                    catch (ArgumentException) {
                    }
                }
            }

            private int GetRandomPort() {
                // TODO: Ensure the port is unused
                return new Random((int)DateTime.Now.Ticks).Next(1025, 65535);
            }
        }
    }
}
