using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Http;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Services.Diagnostics
{
    [LowerCaseFormatterConfig]
    public class RuntimeController : ApiController
    {
        private const string VersionKey = "version";
        private static readonly Regex _versionRegex = new Regex(@"^\d+\.\d+", RegexOptions.ExplicitCapture);
        private readonly ITracer _tracer;

        public RuntimeController(ITracer tracer)
        {
            _tracer = tracer;
        }

        [HttpGet]
        public RuntimeInfo GetRuntimeVersions()
        {
            using (_tracer.Step("RuntimeController.GetRuntimeVersions"))
            {
                return new RuntimeInfo
                {
                    NodeVersions = GetNodeVersions()
                };
            }
        }

        private static IEnumerable<Dictionary<string, string>> GetNodeVersions()
        {
            string nodeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs");
            var directoryInfo = FileSystemHelpers.DirectoryInfoFromDirectoryName(nodeRoot);
            if (directoryInfo.Exists)
            {
                return directoryInfo.GetDirectories()
                                    .Where(dir => _versionRegex.IsMatch(dir.Name))
                                    .Select(dir => new Dictionary<string, string>
                                    {
                                        { VersionKey, dir.Name },
                                        { "npm", TryReadNpmVersion(dir) }
                                    });
            }
            return Enumerable.Empty<Dictionary<string, string>>();
        }

        private static string TryReadNpmVersion(DirectoryInfoBase nodeDir)
        {
            var npmRedirectionFile = nodeDir.GetFiles("npm.txt").FirstOrDefault();
            if (npmRedirectionFile == null)
            {
                return null;
            }
            using (StreamReader reader = new StreamReader(npmRedirectionFile.OpenRead()))
            {
                return reader.ReadLine();
            }
        }
    }
}
