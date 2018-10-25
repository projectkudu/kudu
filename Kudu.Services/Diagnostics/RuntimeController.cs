using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Http;
using Kudu.Contracts.Tracing;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.SiteExtensions;
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
        public RuntimeInfo GetRuntimeVersions(bool allVersions = false)
        {
            using (_tracer.Step("RuntimeController.GetRuntimeVersions"))
            {
                string osName = string.Empty;
                if(OSDetector.IsOnWindows())
                {
                    osName = Environment.OSVersion.Version.Major < 10 ? "Windows Server 2012" : "Windows Server 2016";
                }
                else
                {
                    osName = Environment.OSVersion.VersionString;
                }
                return new RuntimeInfo
                {
                    NodeVersions = GetNodeVersions(allVersions),
                    DotNetCore32 = GetDotNetCoreVersions(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86))),
                    DotNetCore64 = GetDotNetCoreVersions(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))),
                    AspNetCoreModule = GetAspNetCoreModuleVersions(),
                    System = new
                    {
                        os_name = osName,
                        os_build_lab_ex = Microsoft.Win32.Registry.GetValue(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion",
                            "BuildLabEx", null),
                        cores = Environment.ProcessorCount,
                    }
                };
            }
        }

        private static IEnumerable<Dictionary<string, string>> GetNodeVersions(bool allVersions)
        {
            string nodeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs");
            var directoryInfo = FileSystemHelpers.DirectoryInfoFromDirectoryName(nodeRoot);
            if (directoryInfo.Exists)
            {
                return directoryInfo.GetDirectories()
                                    // filter out symbolic link
                                    .Where(dir => allVersions || (dir.Attributes & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
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

        private static object GetDotNetCoreVersions(string folder)
        {
            return new
            {
                shared = new DotNetCoreSharedFrameworksInfo
                {
                    NetCoreApp = GetOrderedVersionStringsFromFolder(Path.Combine(folder, "dotnet", "shared", "Microsoft.NETCore.App")),
                    AspNetCoreAll = GetOrderedVersionStringsFromFolder(Path.Combine(folder, "dotnet", "shared", "Microsoft.AspNetCore.All")),
                    AspNetCoreApp = GetOrderedVersionStringsFromFolder(Path.Combine(folder, "dotnet", "shared", "Microsoft.AspNetCore.App"))
                },
                sdk = GetOrderedVersionStringsFromFolder(Path.Combine(folder, "dotnet", "sdk"))
            };
        }

        private static IEnumerable<string> GetOrderedVersionStringsFromFolder(string folder)
        {
            return GetSemanticVersionsFromFolder(folder)
                .OrderBy(semver => semver)
                .Select(semver => semver.ToString());
        }

        private static IEnumerable<SemanticVersion> GetSemanticVersionsFromFolder(string folder)
        {
            var directoryInfo = FileSystemHelpers.DirectoryInfoFromDirectoryName(folder);
            if (directoryInfo.Exists)
            {
                foreach (var dir in directoryInfo.GetDirectories())
                {
                    if (SemanticVersion.TryParse(dir.Name, out SemanticVersion semver))
                    {
                        yield return semver;
                    }
                }
            }
        }

        private static Dictionary<string,string> GetAspNetCoreModuleVersions()
        {
            var versions = new Dictionary<string, string>();

            AddAspNetCoreModuleVersionIfExists(
                versions,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"inetsrv\aspnetcore.dll"),
                "aspnetcoremodule");
            AddAspNetCoreModuleVersionIfExists(
                versions,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"),
                "aspnetcoremoduleV2");

            return versions;
        }

        private static void AddAspNetCoreModuleVersionIfExists(Dictionary<string, string> versions, string filePath, string keyName)
        {
            var fileInfo = FileSystemHelpers.FileInfoFromFileName(filePath);
            if (fileInfo != null && fileInfo.Exists)
            {
                var fvi = FileVersionInfo.GetVersionInfo(fileInfo.FullName);
                versions[keyName] = fvi.FileVersion;
            }
        }
    }
}
