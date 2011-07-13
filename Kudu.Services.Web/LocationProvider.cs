using System.IO;
using System.Web;

namespace Kudu.Services.Web {
    public class LocationProvider : ILocationProvider {
        public string RepositoryRoot {
            get {
                string path = Path.Combine(GetRootPath(), @"repository");
                EnsureDirectory(path);
                return path;
            }
        }

        private string GetRootPath() {
            return Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "_root");
        }

        private void EnsureDirectory(string path) {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
        }
    }
}