namespace nji
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Ionic;
    using SimpleJson;
    using System.Text.RegularExpressions;
    using Kudu.Core.Deployment;

    class Program
    {
        public string ModulesDir;
        public string TempDir;
        public ILogger Logger { get; set; }
        public Action<string> UpdateStatusText { get; set; }

        public Program()
        {
            ModulesDir = ".\\node_modules";
            TempDir = Path.Combine(ModulesDir, ".tmp");
        }

        void Main(string[] args)
        {
            CleanUpDir(TempDir);
            if (args.Length < 1)
                Usage();
            if (args[0] == "install")
            {
                if (args.Length < 2)
                    Usage();
                foreach (var i in args.Skip(1))
                {
                    // handle both "package@1.2" and just "package" (implies "package@latest")
                    string package, version;
                    var split = i.Split(new[] { '@' }, 2);
                    package = split[0];
                    version = split.Length > 1 ? split[1] : "latest";
                    Install(package, version);
                }
            }
            else if (args[0] == "deps")
            {
                Deps();
            }
            else if (args[0] == "update")
            {
                Update();
            }
            else
            {
                Usage();
            }

            CleanUpDir(TempDir);
            Console.WriteLine("All done");
        }

        private void Update()
        {
            var pkgs = GetInstalled();
            foreach (var pkg in pkgs)
            {
                var meta = GetMetaDataForPkg((string)pkg["name"], "latest");
                if (meta["version"].ToString() != pkg["version"].ToString())
                {
                    Install((string)pkg["name"], "latest");
                }
            }
        }

        private IList<IDictionary<string, object>> GetInstalled()
        {
            var dirs = Directory.GetDirectories(ModulesDir);
            var meta = new List<IDictionary<string, object>>();
            foreach (var dir in dirs)
            {
                var d = Path.Combine(dir, "package.json");
                if (!File.Exists(d))
                    continue;
                var data = File.ReadAllText(d);
                meta.Add((IDictionary<string, object>)SimpleJson.DeserializeObject(data));
            }
            return meta;
        }

        private void Deps()
        {
            InstallDependencies(Environment.CurrentDirectory);
            Console.WriteLine("Dependencies done");
        }

        private void CleanUpDir(string tempDir)
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        private void Install(string pkg, string version)
        {
            // Installs pkg(s) into ./node_modules
            var meta = GetMetaDataForPkg(pkg, version);
            var destPath = SaveAndExtractPackage(meta);
            InstallDependencies(destPath);
        }

        public void InstallDependencies(string pkgDir)
        {
            // Recursively install dependencies
            var s = pkgDir.Split('\\');
            var packageJson = Path.Combine(pkgDir, "package.json");
            if (!File.Exists(packageJson))
                return;
            Console.WriteLine("Checking dependencies for {0} ...", s[s.Length - 1]);

            var metaData = (IDictionary<string, object>)SimpleJson.DeserializeObject(File.ReadAllText(packageJson));
            if (metaData.ContainsKey("dependencies") && !(metaData["dependencies"] is IList<object>))
            {
                foreach (var dep in (IDictionary<string, object>)metaData["dependencies"])
                {
                    // Don't redownload packaged we already have
                    if (Directory.Exists(Path.Combine(ModulesDir, dep.Key)))
                    {
                        continue;
                    }
                    Install(dep.Key, dep.Value as string);
                }
            }
        }

        private string SaveAndExtractPackage(IDictionary<string, object> meta)
        {
            var pkgName = (string)meta["name"];
            var destPath = Path.Combine(ModulesDir, pkgName);
            var url = (string)((IDictionary<string, object>)meta["dist"])["tarball"];
            var urlSplit = url.Split('/');
            var filename = urlSplit[urlSplit.Length - 1];
            var tmpFilePath = Path.Combine(TempDir, filename);
            if (File.Exists(tmpFilePath)) // make sure we don't re-download and reinstall anything
                return destPath;
            UpdateStatusText(String.Format("Installing {0}", pkgName));
            Logger.Log("Installing {0} into {1} ...", url, destPath);
            CleanUpDir(destPath);
            if (Directory.Exists(destPath))
                Directory.Delete(destPath, true);
            if (!Directory.Exists(TempDir))
                Directory.CreateDirectory(TempDir);
            var tempPkgDir = Path.Combine(TempDir, pkgName);
            if (!Directory.Exists(tempPkgDir))
                Directory.CreateDirectory(tempPkgDir);

            new WebClient().DownloadFile(url, tmpFilePath);

            var workingDir = Environment.CurrentDirectory;
            Environment.CurrentDirectory = tempPkgDir;

            Tar.Extract(Path.Combine(workingDir, tmpFilePath));
            Environment.CurrentDirectory = workingDir;
            Directory.Move(Directory.GetDirectories(tempPkgDir)[0], destPath);
            return destPath;
        }

        private IDictionary<string, object> GetMetaDataForPkg(string pkg, string version)
        {
            version = version ?? "latest";
            if (!Regex.Match(version, @"^[\.\da-zA-Z]*$").Success)
            {
                Logger.Log(String.Format("Not smart enough to understand version '{0}', so using 'latest' instead for package '{1}'.", version, pkg), LogEntryType.Warning);
                version = "latest";
            }
            var url = string.Format("http://registry.npmjs.org/{0}/{1}", pkg, version);
            string response = null;
            try
            {
                response = new WebClient().DownloadString(url);
            }
            catch
            {
                Logger.Log(String.Format("No module named {0} in package registry! Aborting!", pkg), LogEntryType.Error);
                throw;
            }
            return (IDictionary<string, object>)SimpleJson.DeserializeObject(response);
        }

        private void Usage()
        {
            Console.WriteLine();
            Console.WriteLine(@"
Usage:
 nji deps                  - Install dependencies from package.json file
 nji install <pkg> [<pkg>] - Install package(s), and it's/there dependencies
 nji update                - Checks for different version of packages in online repository, and updates as needed
Example:
 nji install express socket.io mongolian underscore");

            Environment.Exit(0);
        }
    }
}