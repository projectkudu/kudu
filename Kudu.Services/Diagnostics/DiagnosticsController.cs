using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.Tracing;
using Kudu.Services.Diagnostics;
using Kudu.Services.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Kudu.Services.Performance
{
    public class DiagnosticsController : ApiController
    {
        // Matches Docker log filenames of logs that haven't been rolled (are most current for a given machine name)
        private static readonly Regex NONROLLED_DOCKER_LOG_FILENAME_REGEX = new Regex(@"^\d{4}_\d{2}_\d{2}_.*_docker\.log$");

        private readonly DiagnosticsSettingsManager _settingsManager;
        private readonly string[] _paths;
        private readonly ITracer _tracer;
        private readonly IApplicationLogsReader _applicationLogsReader;

        public DiagnosticsController(IEnvironment environment, ITracer tracer, IApplicationLogsReader applicationLogsReader)
        {
            // Setup the diagnostics service to collect information from the following paths:
            // 1. The deployments folder
            // 2. The profile dump
            // 3. The npm log
            _paths = new[] {
                environment.DeploymentsPath,
                Path.Combine(environment.RootPath, Constants.LogFilesPath),
                Path.Combine(environment.WebRootPath, Constants.NpmDebugLogFile),
            };

            _applicationLogsReader = applicationLogsReader;
            _tracer = tracer;
            _settingsManager = new DiagnosticsSettingsManager(Path.Combine(environment.DiagnosticsPath, Constants.SettingsJsonFile), _tracer);
        }

        /// <summary>
        /// Get all the diagnostic logs as a zip file
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [SuppressMessage("Microsoft.Usage", "CA2202", Justification = "The ZipArchive is instantiated in a way that the stream is not closed on dispose")]
        public HttpResponseMessage GetLog()
        {
            HttpResponseMessage response = Request.CreateResponse();
            response.Content = ZipStreamContent.Create(String.Format("dump-{0:MM-dd-HH-mm-ss}.zip", DateTime.UtcNow), _tracer, zip =>
            {
                AddFilesToZip(zip);
            });
            return response;
        }

        [HttpGet]
        public HttpResponseMessage GetRecentLogs(int top = 100)
        {
            using (_tracer.Step("DiagnosticsController.GetRecentLogs"))
            {
                var results = _applicationLogsReader.GetRecentLogs(top);
                return Request.CreateResponse(HttpStatusCode.OK, results);
            }
        }

        // Route only exists for this on Linux
        // Grabs "currently relevant" Docker logs from the LogFiles folder
        // and returns a JSON response with links to the files in the VFS API
        [HttpGet]
        public HttpResponseMessage GetDockerLogs()
        {
            using (_tracer.Step("DiagnosticsController.GetDockerLogs"))
            {
                var currentDockerLogFilenames = GetCurrentDockerLogFilenames();

                // TODO Can we read the vfs route string from somewhere instead of hard-coding "/api/vfs"?
                var vfsBaseAddress = Request.RequestUri.GetComponents(UriComponents.Scheme | UriComponents.Host, UriFormat.UriEscaped) + "/api/vfs";

                var responseContent = currentDockerLogFilenames.Select(p => CurrentDockerLogFilenameToJson(p, vfsBaseAddress));

                return Request.CreateResponse(HttpStatusCode.OK, responseContent);
            }
        }

        // Route only exists for this on Linux
        // Grabs "currently relevant" Docker logs from the LogFiles folder
        // and returns them in a zip archive
        [HttpGet]
        [SuppressMessage("Microsoft.Usage", "CA2202", Justification = "The ZipArchive is instantiated in a way that the stream is not closed on dispose")]
        public HttpResponseMessage GetDockerLogsZip()
        {
            using (_tracer.Step("DiagnosticsController.GetDockerLogsZip"))
            {
                var currentDockerLogFilenames = GetCurrentDockerLogFilenames().ToArray();

                HttpResponseMessage response = Request.CreateResponse();
                response.Content = ZipStreamContent.Create(String.Format("dockerlogs-{0:MM-dd-HH-mm-ss}.zip", DateTime.UtcNow), _tracer, zip =>
                {
                    foreach (var filename in currentDockerLogFilenames)
                    {
                        zip.AddFile(filename, _tracer);
                    }
                });
                return response;
            }
        }

        private IEnumerable<string> GetCurrentDockerLogFilenames()
        {
            //TODO get from environment
            var path = "/home/LogFiles";

            var nonRolledDockerLogFilenames =
                FileSystemHelpers.GetFiles(path, "*", SearchOption.TopDirectoryOnly)
                .Where(f => NONROLLED_DOCKER_LOG_FILENAME_REGEX.IsMatch(Path.GetFileName(f)))
                .ToArray();

            // Find the latest date stamp and filter out those that don't have it
            var latestDatestamp = nonRolledDockerLogFilenames
                .Select(p => Path.GetFileName(p).Substring(0, 10))
                .OrderByDescending(s => int.Parse(s.Replace("_", String.Empty)))
                .First();

            return nonRolledDockerLogFilenames.Where(f => Path.GetFileName(f).StartsWith(latestDatestamp));
        }

        private JObject CurrentDockerLogFilenameToJson(string path, string vfsBaseAddress)
        {
            var info = new FileInfo(path);

            // Machine name is the middle portion of the filename, between the datestamp prefix
            // and the _docker.log suffix.
            var machineName = info.Name.Substring(11, info.Name.Length - 22);

            // TODO This should be more generalized; should be able to get root from environment
            // Remove "/home" from the FullName, as it's implicit in the vfs url
            var vfsPath = info.FullName.Remove(0, "/home".Length);

            var vfsUrl = (vfsBaseAddress + Uri.EscapeUriString(vfsPath)).EscapeHashCharacter();

            return new JObject(
                new JProperty("machineName", machineName),
                new JProperty("lastUpdated", info.LastWriteTimeUtc),
                new JProperty("size", info.Length),
                new JProperty("href", vfsUrl),
                new JProperty("path", info.FullName));
        }

        private void AddFilesToZip(ZipArchive zip)
        {
            foreach (var path in _paths)
            {
                if (Directory.Exists(path))
                {
                    var dir = new DirectoryInfo(path);
                    if (path.EndsWith(Constants.LogFilesPath, StringComparison.Ordinal))
                    {
                        foreach (var info in dir.GetFileSystemInfos())
                        {
                            var directoryInfo = info as DirectoryInfo;
                            if (directoryInfo != null)
                            {
                                // excluding FREB as it contains user sensitive data such as authorization header
                                if (!info.Name.StartsWith("W3SVC", StringComparison.OrdinalIgnoreCase))
                                {
                                    zip.AddDirectory(directoryInfo, _tracer, Path.Combine(dir.Name, info.Name));
                                }
                            }
                            else
                            {
                                zip.AddFile((FileInfo)info, _tracer, dir.Name);
                            }
                        }
                    }
                    else
                    {
                        zip.AddDirectory(dir, _tracer, Path.GetFileName(path));
                    }
                }
                else if (File.Exists(path))
                {
                    zip.AddFile(path, _tracer, String.Empty);
                }
            }
        }

        public HttpResponseMessage Set(DiagnosticsSettings newSettings)
        {
            if (newSettings == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            _settingsManager.UpdateSettings(newSettings);

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        public HttpResponseMessage Delete(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            _settingsManager.DeleteSetting(key);

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        public HttpResponseMessage GetAll()
        {
            return Request.CreateResponse(HttpStatusCode.OK, _settingsManager.GetSettings());
        }

        public HttpResponseMessage Get(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            object value = _settingsManager.GetSetting(key);

            if (value == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, String.Format(Resources.SettingDoesNotExist, key));
            }

            return Request.CreateResponse(HttpStatusCode.OK, value);
        }
    }
}
