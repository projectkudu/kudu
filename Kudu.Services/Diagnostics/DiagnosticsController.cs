using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Services.Diagnostics;
using Kudu.Services.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Performance
{
    public class DiagnosticsController : ApiController
    {
        private readonly JsonSettings _settings;
        private readonly string[] _paths;
        private readonly ITracer _tracer;
        private readonly IApplicationLogsReader _applicationLogsReader;

        public DiagnosticsController(IEnvironment environment, IFileSystem fileSystem, ITracer tracer, IApplicationLogsReader applicationLogsReader)
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

            _settings = new JsonSettings(fileSystem, Path.Combine(environment.DiagnosticsPath, Constants.SettingsJsonFile));
            _applicationLogsReader = applicationLogsReader;
            _tracer = tracer;
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

        public HttpResponseMessage Set(JObject newSettings)
        {
            if (newSettings == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            _settings.SetValues(newSettings);

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        public HttpResponseMessage Delete(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            _settings.DeleteValue(key);

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        public HttpResponseMessage GetAll()
        {
            return Request.CreateResponse(HttpStatusCode.OK, _settings.GetValues());
        }

        public HttpResponseMessage Get(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            string value = _settings.GetValue(key);

            if (value == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, String.Format(Resources.SettingDoesNotExist, key));
            }

            return Request.CreateResponse(HttpStatusCode.OK, value);
        }
    }
}
