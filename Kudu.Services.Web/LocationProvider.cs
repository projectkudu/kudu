using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using System.IO;

namespace Kudu.Services.Web {
    public class LocationProvider : ILocationProvider {
        public string RepositoryRoot {
            get {
                return Path.Combine(GetRootPath(), @"repository");
            }
        }

        private string GetRootPath() {
            // Temporary path (under bin folder so we don't need to ignore it in source control)
            string path = Path.Combine(HttpRuntime.BinDirectory, "_root");

            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }
}