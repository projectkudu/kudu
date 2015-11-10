using Kudu.Contracts.Diagnostics;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace Kudu.Services.Diagnostics
{
    public class CrashDumpController : ApiController
    {
        private readonly ITracer _tracer;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;

        public CrashDumpController(ITracer tracer,
                                 IEnvironment environment,
                                 IDeploymentSettingsManager settings)
        {
            _tracer = tracer;
            _environment = environment;
            _settings = settings;
        }

        [HttpGet]
        public IEnumerable<CrashDumpInfo> List()
        {
            using (_tracer.Step("CrashDumpController.List"))
            {
                return FileSystemHelpers.GetFiles(_environment.CrashDumpsPath, "*.dmp")
                    .Select(FileSystemHelpers.FileInfoFromFileName)
                    .Select(f => GetCrashDumpInfoFromFile(f, Request.RequestUri.GetLeftPart(UriPartial.Authority)));
            }
        }

        [HttpGet]
        public HttpResponseMessage Get(string name)
        {
            using (_tracer.Step($"CrashDumpController.Get({name})"))
            {
                return Request.CreateResponse(HttpStatusCode.OK, GetCrashDump(name));
            }
        }

        [HttpDelete]
        public HttpResponseMessage Delete(string name)
        {
            using (_tracer.Step($"CrashDumpController.Delete({name})"))
            {
                var crashDump = GetCrashDump(name);
                FileSystemHelpers.DeleteFile(crashDump.FilePath);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> Analyze(string name)
        {
            using (_tracer.Step($"CrashDumpController.Analyze({name})"))
            {
                var crashDump = GetCrashDump(name);
                var exe = new Executable(ResolveCdbPath(), _environment.RootPath, _settings.GetCommandIdleTimeout());
                using (MemoryStream outputStream = new MemoryStream(), errorStream = new MemoryStream())
                {
                    // ExecuteAsync could deadlock if called from an ASP.NET thread
                    var exitCode = await Task.Run(async () => await exe.ExecuteAsync(_tracer, $"-c \".symfix;.reload;.loadby sos clr;~*k;!analyze -v;q\" -z \"{crashDump.FilePath}\"", outputStream, errorStream));
                    if (exitCode == 0)
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new { output = outputStream.AsString() });
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.InternalServerError, new { error = errorStream.AsString() });
                    }
                }
            }
        }

        //public for unit tests
        public CrashDumpInfo GetCrashDump(string name)
        {
            var path = Path.Combine(_environment.CrashDumpsPath, name);
            if (FileSystemHelpers.FileExists(path))
            {
                var fileInfo = FileSystemHelpers.FileInfoFromFileName(path);
                return GetCrashDumpInfoFromFile(fileInfo, Request.RequestUri.GetLeftPart(UriPartial.Authority));
            }
            throw new HttpResponseException(HttpStatusCode.NotFound);
        }

        private static CrashDumpInfo GetCrashDumpInfoFromFile(FileInfoBase fileInfo, string baseUrl)
        {
            baseUrl = baseUrl.TrimEnd('/');
            return new CrashDumpInfo
            {
                Name = fileInfo.Name,
                Timestamp = fileInfo.LastWriteTime,
                Href = new Uri($"{baseUrl}/api/crashdumps/{fileInfo.Name}"),
                AnalyizeHref = new Uri($"{baseUrl}/api/crashdumps/{fileInfo.Name}/analyze"),
                DownloadHref = new Uri($"{baseUrl}/api/vfs/data/{Constants.Dumps}/{fileInfo.Name}"),
                FilePath = fileInfo.FullName
            };
        }

        private static string ResolveCdbPath()
        {
            var programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "Windows Kits", "8.1", "Debuggers", "x64", "cdb.exe");
        }
    }
}
