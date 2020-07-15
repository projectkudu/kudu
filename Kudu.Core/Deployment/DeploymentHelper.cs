using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.Tracing;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using System.Net.Http;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public static class DeploymentHelper
    {
        // build using msbuild in Azure
        // does not include njsproj(node), pyproj(python), they are built differently
        private static readonly string[] _projectFileExtensions = new[] { ".csproj", ".vbproj", ".fsproj", ".xproj" };

        public static readonly string[] ProjectFileLookup = _projectFileExtensions.Select(p => "*" + p).ToArray();

        public static IList<string> GetMsBuildProjects(string path, IFileFinder fileFinder, SearchOption searchOption = SearchOption.AllDirectories)
        {
            IEnumerable<string> filesList = fileFinder.ListFiles(path, searchOption, ProjectFileLookup);
            return filesList.ToList();
        }

        public static bool IsMsBuildProject(string path)
        {
            return _projectFileExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsDefaultWebRootContent(string webroot)
        {
            if (!FileSystemHelpers.DirectoryExists(webroot))
            {
                // degenerated
                return true;
            }

            var entries = FileSystemHelpers.GetFileSystemEntries(webroot);
            if (entries.Length == 0)
            {
                // degenerated
                return true;
            }

            if (entries.Length == 1 && FileSystemHelpers.FileExists(entries[0]))
            {
                string hoststarthtml = Path.Combine(webroot, Constants.HostingStartHtml);
                return String.Equals(entries[0], hoststarthtml, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public static void PurgeZipsIfNecessary(string sitePackagesPath, ITracer tracer, int totalAllowedZips)
        {
            IEnumerable<string> zipFiles = FileSystemHelpers.GetFiles(sitePackagesPath, "*.zip");
            if (zipFiles.Count() > totalAllowedZips)
            {
                // Order the files in descending order of the modified date and remove the last (N - allowed zip files).
                var fileNamesToDelete = zipFiles.OrderByDescending(fileName => FileSystemHelpers.GetLastWriteTimeUtc(fileName)).Skip(totalAllowedZips);
                foreach (var fileName in fileNamesToDelete)
                {
                    using (tracer.Step("Deleting outdated zip file {0}", fileName))
                    {
                        try
                        {
                            FileSystemHelpers.DeleteFile(fileName);
                        }
                        catch (Exception ex)
                        {
                            tracer.TraceError(ex, "Unable to delete zip file {0}", fileName);
                        }
                    }
                }
            }
        }

        public static async Task<HttpContent> GetZipContentFromURL(ZipDeploymentInfo zipDeploymentInfo, ITracer tracer)
        {
            using (var client = new HttpClient(new HttpClientHandler()))
            {
                Uri uri = new Uri(zipDeploymentInfo.ZipURL);
                using (tracer.Step($"Trying to make a GET request to {StringUtils.ObfuscatePath(zipDeploymentInfo.ZipURL)}"))
                {
                    try
                    {
                        return await OperationManager.AttemptAsync<HttpContent>(async () =>
                        {
                            HttpResponseMessage response = await client.GetAsync(uri);
                            response.EnsureSuccessStatusCode();
                            return response.Content;
                        });
                    }
                    catch (Exception ex)
                    {
                        tracer.TraceError(ex, "Could not make a successful GET request to {0}", uri.AbsoluteUri);
                        throw;
                    }
                }
            }
        }
    }
}
