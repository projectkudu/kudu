using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http.Formatting;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Http.Controllers;
using Kudu.Contracts.Tracing;
using Newtonsoft.Json;

namespace Kudu.Services.Diagnostics
{
    public class RuntimeController : ApiController
    {
        private const string VersionKey = "version";
        private static readonly Regex _versionRegex = new Regex(@"^\d+\.\d+", RegexOptions.ExplicitCapture);
        private readonly ITracer _tracer;
        private readonly IFileSystem _fileSystem;

        public RuntimeController(ITracer tracer, IFileSystem fileSystem)
        {
            _tracer = tracer;
            _fileSystem = fileSystem;
        }

        protected override void Initialize(HttpControllerContext controllerContext)
        {
            controllerContext.Configuration.Formatters.Clear();

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new LowerCasePropertyNamesContractResolver()
            };
            controllerContext.Configuration.Formatters.Add(new JsonMediaTypeFormatter { SerializerSettings = settings });
        }

        [HttpGet]
        public RuntimeInfo GetRuntimeVersions()
        {
            using (_tracer.Step("RuntimeController.GetRuntimeVersions"))
            {
                return new RuntimeInfo
                {
                    NodeVerions = GetNodeVersions()
                };
            }
        }

        private IEnumerable<Dictionary<string, string>> GetNodeVersions()
        {
            string nodeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs");
            var directoryInfo = _fileSystem.DirectoryInfo.FromDirectoryName(nodeRoot);
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
