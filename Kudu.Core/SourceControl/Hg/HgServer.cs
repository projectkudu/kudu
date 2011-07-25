using System;
using System.Threading;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.SourceControl.Hg {
    public class HgServer {
        private readonly Executable _hgExe;
        private readonly string _appName;
        private readonly string _configFile;

        internal HgServer(Infrastructure.Executable hgExe, string appName, string configFile) {
            _hgExe = hgExe;
            _appName = appName;
            _configFile = configFile;
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

                    // Start the server
                    _hgExe.Execute(@"serve -d --port {0} --web-conf ""{1}""", Port, _configFile);

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
            if (IsRunning) {
                return;
            }

            _hgExe.Kill();
        }

        private int GetRandomPort() {
            // TODO: Ensure the port is unused
            return new Random((int)DateTime.Now.Ticks).Next(1025, 65535);
        }
    }
}
