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
using Kudu.Core.Helpers;

namespace Kudu.Services.Performance
{
    public class DiagnosticsController : ApiController
    {
        // Matches Container log filenames of logs that haven't been rolled (are most current for a given machine name)
        // Format is YYYY_MM_DD_<machinename>_(docker|app|platform|console)[.<roll_number>].log
        // Examples:
        //   Linux --
        //   2017_08_23_RD00155DD0D38E_docker.log (not rolled)
        //   2017_08_23_RD00155DD0D38E_docker.1.log (rolled)
        //   Windows Containers --
        //   2020_06_09_xn0ldwk000000_platform.log
        //   2020_06_09_xn0ldwk000000_console.log
        //   2020_06_09_xn0ldwk000000_app.log
        private static readonly Regex NONROLLED_CONTAINER_LOG_FILENAME_REGEX = new Regex(@"^\d{4}_\d{2}_\d{2}_.*_(docker|app|platform|console)\.log$");

        private readonly DiagnosticsSettingsManager _settingsManager;
        private readonly string[] _paths;
        private readonly ITracer _tracer;
        private readonly IApplicationLogsReader _applicationLogsReader;
        private readonly IEnvironment _environment;

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

            _environment = environment;
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

        // Route exists for this on Linux and Windows
        // Grabs "currently relevant" logs from the LogFiles folder
        // and returns a JSON response with links to the files in the VFS API
        [HttpGet]
        public HttpResponseMessage GetContainerLogs(HttpRequestMessage request)
        {
            using (_tracer.Step("DiagnosticsController.GetContainerLogs"))
            {
                var currentContainerLogFilenames = GetCurrentContainerLogFilenames(SearchOption.TopDirectoryOnly);

                var vfsBaseAddress = UriHelper.MakeRelative(UriHelper.GetBaseUri(request), "api/vfs");

                // Open files in order to refresh (not update) the timestamp and file size.
                // This is needed on Linux due to the way that metadata for files on the CIFS
                // mount gets cached and not always refreshed. Limit to 10 as a safety.
                if (!OSDetector.IsOnWindows())
                {
                    foreach (var filename in currentContainerLogFilenames.Take(10))
                    {
                        using (var file = File.OpenRead(filename))
                        {
                            // This space intentionally left blank
                        }
                    }
                }

                var responseContent = currentContainerLogFilenames.Select(p => CurrentContainerLogFilenameToJson(p, vfsBaseAddress.ToString()));

                return Request.CreateResponse(HttpStatusCode.OK, responseContent);
            }
        }

        // Route exists for this on Linux and Windows
        // Grabs "currently relevant" Container logs from the LogFiles folder
        // and returns them in a zip archive
        [HttpGet]
        [SuppressMessage("Microsoft.Usage", "CA2202", Justification = "The ZipArchive is instantiated in a way that the stream is not closed on dispose")]
        public HttpResponseMessage GetContainerLogsZip()
        {
            using (_tracer.Step("DiagnosticsController.GetContainerLogsZip"))
            {
                var currentContainerLogFilenames = GetCurrentContainerLogFilenames(SearchOption.TopDirectoryOnly);

                HttpResponseMessage response = Request.CreateResponse();
                response.Content = ZipStreamContent.Create(String.Format("containerlogs-{0:MM-dd-HH-mm-ss}.zip", DateTime.UtcNow), _tracer, zip =>
                {
                    foreach (var filename in currentContainerLogFilenames)
                    {
                        zip.AddFile(filename, _tracer);
                    }
                });
                return response;
            }
        }

        private string[] GetCurrentContainerLogFilenames(SearchOption searchOption)
        {
            // Get all non-rolled Container log filenames from the LogFiles directory
            var nonRolledContainerLogFilenames =
                FileSystemHelpers.ListFiles(_environment.LogFilesPath, searchOption, new[] { "*" })
                .Where(f => NONROLLED_CONTAINER_LOG_FILENAME_REGEX.IsMatch(Path.GetFileName(f)))
                .ToArray();

            if (!nonRolledContainerLogFilenames.Any())
            {
                return new string[0];
            }

            // Find the latest date stamp and filter out those that don't have it
            // Timestamps are YYYY_MM_DD (sortable as integers with the underscores removed)
            var latestDatestamp = nonRolledContainerLogFilenames
                .Select(p => Path.GetFileName(p).Substring(0, 10))
                .OrderByDescending(s => int.Parse(s.Replace("_", String.Empty)))
                .First();

            return nonRolledContainerLogFilenames
                .Where(f => Path.GetFileName(f).StartsWith(latestDatestamp, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private JObject CurrentContainerLogFilenameToJson(string path, string vfsBaseAddress)
        {
            var info = new FileInfo(path);

            // Log File Format : YYYY_MM_DD_<machinename>_(docker|app|platform|console)[.<roll_number>].log
            // Length(YYYY_MM_DD_) = 11
            var machineName = info.Name.Substring(11, info.Name.IndexOf('_', 11) - 11);

            // Remove the root path from the front of the FullName, as it's implicit in the vfs url
            var vfsPath = info.FullName.Remove(0, _environment.RootPath.Length);

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
