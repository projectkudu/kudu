using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public static class PythonSiteEnabler
    {
        private const string RequirementsFileName = "requirements.txt";
        private const string RuntimeFileName = "runtime.txt";
        private const string PythonFileExtension = "*.py";

        public static bool LooksLikePython(string siteFolder)
        {
            if (!OSDetector.IsOnWindows())
            {
                // For Linux web apps: Rely on WEBSITE_PYTHON_VERSION environment variable to
                // detect if this is a python app
                string pythonVersion = System.Environment.GetEnvironmentVariable("WEBSITE_PYTHON_VERSION");
                if (!string.IsNullOrEmpty(pythonVersion) && pythonVersion.StartsWith("3", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }

            var reqsFilePath = Path.Combine(siteFolder, RequirementsFileName);
            if (!FileSystemHelpers.FileExists(reqsFilePath))
            {
                return false;
            }

            var pythonFiles = FileSystemHelpers.GetFiles(siteFolder, PythonFileExtension);
            if (pythonFiles.Length > 0)
            {
                return true;
            }

            // Most Python sites will have at least a .py file in the root, but
            // some may not. In that case, let them opt in with the runtime.txt
            // file, which is used to specify the version of Python.
            var runtimeFilePath = Path.Combine(siteFolder, RuntimeFileName);
            if (FileSystemHelpers.FileExists(runtimeFilePath))
            {
                try
                {
                    var text = FileSystemHelpers.ReadAllTextFromFile(runtimeFilePath);
                    return text.IndexOf("python", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch (IOException)
                {
                    return false;
                }
            }

            return false;
        }
    }
}
