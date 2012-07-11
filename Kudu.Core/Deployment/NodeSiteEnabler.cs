using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class NodeSiteEnabler
    {
        private IFileSystem _fileSystem;
        private string _siteFolder;
        private string _repoFolder;
        private string _scriptPath;
        private readonly string[] NodeStartFiles = new[] { "server.js", "app.js" };
        private readonly string[] NonNodeExtensions = new[] { "*.php", "*.htm", "*.html", "*.aspx", "*.cshtml" };
        private const string WebConfigFile = "web.config";
        private const string PackageJsonFile = "package.json";

        public NodeSiteEnabler(IFileSystem fileSystem, string repoFolder, string siteFolder, string scriptPath)
        {
            _fileSystem = fileSystem;
            _repoFolder = repoFolder;
            _siteFolder = siteFolder;
            _scriptPath = scriptPath;
        }

        public bool NeedNodeHandling()
        {
            // If there is a config file in the repo, we don't need to do anything
            if (_fileSystem.File.Exists(Path.Combine(_repoFolder, WebConfigFile)))
            {
                return false;
            }

            return this.LooksLikeNode();
        }

        public bool LooksLikeNode()
        {
            // If it has package.json at the root, it is node
            if (_fileSystem.File.Exists(Path.Combine(_siteFolder, PackageJsonFile)))
            {
                return true;
            }

            // If it has no .js files at the root, it's not Node
            if (!_fileSystem.Directory.GetFiles(_siteFolder, "*.js").Any())
            {
                return false;
            }

            // If it has a node_modules folder, it's likely Node
            if (_fileSystem.Directory.Exists(Path.Combine(_siteFolder, "node_modules")))
            {
                return true;
            }

            // If it has files that have a clear non-Node extension, treat it as non-Node
            foreach (var extension in NonNodeExtensions)
            {
                if (_fileSystem.Directory.GetFiles(_siteFolder, extension).Any())
                {
                    return false;
                }
            }

            return true;
        }

        public string GetNodeStartFile()
        {
            // Check if any of the known start pages exist
            foreach (var nodeDetectionFile in NodeStartFiles)
            {
                string fullPath = Path.Combine(_siteFolder, nodeDetectionFile);
                if (_fileSystem.File.Exists(fullPath))
                {
                    return nodeDetectionFile;
                }
            }

            return null;
        }

        public void CreateConfigFile(string nodeStartFile)
        {
            _fileSystem.File.WriteAllText(
                Path.Combine(_siteFolder, WebConfigFile),
                String.Format(Resources.IisNodeWebConfig, nodeStartFile));
        }

        public void SelectNodeVersion(ILogger logger)
        {
            // The node.js version selection logic is implemented in selectNodeVersion.js. 

            // run with default node.js version which is on the path
            Executable executor = new Executable("node.exe", string.Empty);
            string result;
            bool success;
            try
            {
                result = executor.Execute(
                    "\"{0}\\selectNodeVersion.js\" \"{1}\" \"{2}\"",
                    _scriptPath,
                    _repoFolder,
                    _siteFolder).Item1;
                success = true;
            }
            catch (Exception e)
            {
                result = e.Message;
                success = false;
            }

            if (!string.IsNullOrEmpty(result))
            {
                result.Split('\n').ToList().ForEach(line =>
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        logger.Log(line);
                    }
                });
            }
            
            if (!success)
            {
                throw new InvalidOperationException(Resources.Error_UnableToSelectNodeVersion);
            }
        }
    }
}
