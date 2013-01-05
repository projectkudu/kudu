using System;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using Ionic.Zip;
using Kudu.Core;
using Kudu.Core.Settings;
using Kudu.Services.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Performance
{
    public class DiagnosticsController : ApiController
    {
        private readonly JsonSettings _settings;
        private readonly string[] _paths;
        private static object _lockObj = new object();

        public DiagnosticsController(IEnvironment environment, IFileSystem fileSystem)
        {
            // Setup the diagnostics service to collect information from the following paths:
            // 1. The deployments folder
            // 2. The profile dump
            // 3. The npm log
            _paths = new[] { 
                environment.DeploymentCachePath,
                Path.Combine(environment.RootPath, Constants.LogFilesPath),
                Path.Combine(environment.WebRootPath, Constants.NpmDebugLogFile),
            };

            _settings = new JsonSettings(fileSystem, Path.Combine(environment.DiagnosticsPath, Constants.SettingsJsonFile));
        }

        /// <summary>
        /// Get all the diagnostic logs as a zip file
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetLog()
        {
            lock (_lockObj)
            {
                HttpResponseMessage response = Request.CreateResponse();
                using (var zip = new ZipFile())
                {
                    foreach (var path in _paths)
                    {
                        if (Directory.Exists(path))
                        {
                            if (path.EndsWith(Constants.LogFilesPath, StringComparison.OrdinalIgnoreCase))
                            {
                                DirectoryInfo dir = new DirectoryInfo(path);
                                foreach (var info in dir.GetFileSystemInfos())
                                {
                                    if (info is DirectoryInfo)
                                    {
                                        // excluding FREB as it contains user sensitive data such as authorization header
                                        if (!info.Name.StartsWith("W3SVC", StringComparison.OrdinalIgnoreCase))
                                        {
                                            zip.AddDirectory(info.FullName, Path.Combine(dir.Name, info.Name));
                                        }
                                    }
                                    else
                                    {
                                        zip.AddFile(info.FullName, dir.Name);
                                    }
                                }
                            }
                            else
                            {
                                zip.AddDirectory(path, Path.GetFileName(path));
                            }
                        }
                        else if (File.Exists(path))
                        {
                            zip.AddFile(path, String.Empty);
                        }
                    }

                    var ms = new MemoryStream();
                    zip.Save(ms);
                    response.Content = ms.AsContent();
                }
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = String.Format("dump-{0:MM-dd-H:mm:ss}.zip", DateTime.UtcNow);
                return response;
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
