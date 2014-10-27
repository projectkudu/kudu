using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public static class PythonSiteEnabler
    {
        private static readonly string[] PythonDetectionFiles = new[] { "requirements.txt" };

        public static bool LooksLikePython(string siteFolder)
        {
            // If any of the files in PythonDetectionFiles exist
            // We assume it's python
            foreach (var pythonDetectionFile in PythonDetectionFiles)
            {
                string fullPath = Path.Combine(siteFolder, pythonDetectionFile);
                if (FileSystemHelpers.FileExists(fullPath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
