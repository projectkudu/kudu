using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Core.Tracing
{
    public class SiteExtensionLogManager
    {
        private const string DefaultFileNameFormat = "kudu_{0}.log";
        private const int MaxFileSize = 1024 * 1024; // 1 MB
        private const int MaxTotalFilesSize = MaxFileSize * 10;

        private static readonly string SiteExtensionLogSearchPattern = DefaultFileNameFormat.FormatInvariant("*");

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
        {
            CheckAdditionalContent = false,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            MaxDepth = 1,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Error
        };

        private readonly ITracer _tracer;
        private readonly string _directoryPath;
        private string _currentFileName;
        private string _currentPath;

        public SiteExtensionLogManager(ITracer tracer, string directoryPath)
        {
            _tracer = tracer;
            _directoryPath = directoryPath;

            UpdateCurrentPath();
        }

        public void Log(IDictionary<string, object> siteExtensionLogEvent)
        {
            using (_tracer.Step("Site Extension Log"))
            {
                try
                {
                    // TODO: Consider doing cleanup only once per X minutes
                    HandleCleanup();

                    string message = JsonConvert.SerializeObject(siteExtensionLogEvent, JsonSerializerSettings);

                    _tracer.Trace("{0}", message);

                    OperationManager.Attempt(() =>
                    {
                        using (var streamWriter = new StreamWriter(FileSystemHelpers.OpenFile(_currentPath, FileMode.Append, FileAccess.Write, FileShare.Read)))
                        {
                            streamWriter.WriteLine(message);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _tracer.TraceError(ex);
                }
            }
        }

        private void HandleCleanup()
        {
            FileInfoBase[] extentionLogFiles = ListLogFiles();

            if (extentionLogFiles.Sum(file => file.Length) > MaxTotalFilesSize)
            {
                extentionLogFiles.OrderBy(file => file.LastWriteTimeUtc).First().Delete();
            }

            FileInfoBase currentFileInfo = extentionLogFiles.FirstOrDefault(file => String.Equals(file.Name, _currentFileName, StringComparison.OrdinalIgnoreCase));
            if (currentFileInfo != null && currentFileInfo.Length > MaxFileSize)
            {
                UpdateCurrentPath();
            }
        }

        private void UpdateCurrentPath()
        {
            string filePostfix = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            _currentFileName = DefaultFileNameFormat.FormatInvariant(filePostfix);
            _currentPath = Path.Combine(_directoryPath, _currentFileName);
        }

        private FileInfoBase[] ListLogFiles()
        {
            try
            {
                return FileSystemHelpers.DirectoryInfoFromDirectoryName(_directoryPath).GetFiles(SiteExtensionLogSearchPattern);
            }
            catch
            {
                return new FileInfoBase[0];
            }
        }
    }
}
