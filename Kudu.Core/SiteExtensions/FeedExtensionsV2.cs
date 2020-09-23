using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Ionic.Zip;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json.Linq;
using NuGet.Client;

namespace Kudu.Core.SiteExtensions
{
    public static class FeedExtensionsV2
    {
        /// <summary>
        /// HttpClient timeout
        /// </summary>
        public static TimeSpan HttpClientTimeout { get; set; } = DeploymentSettingsExtension.DefaultHttpClientTimeout;

        /// <summary>
        /// Return the feed URL to directly query nuget v2 api by search term, 
        /// always include pre-released
        /// </summary>
        internal static string GetFeedUrl(string filter)
        {
            String searchTerm = "tags:AzureSiteExtension";
            if (String.IsNullOrWhiteSpace(filter))
            {
                // No user provided search string: just search by tag
                searchTerm = "tags:AzureSiteExtension";
            }
            else if (filter.Contains(":"))
            {
                // User provided complex query with fields: add it as is to the tag field query
                searchTerm = $"tags:AzureSiteExtension {filter}";
            }
            else
            {
                // User provided simple string: treat it as a title. This is not ideal behavior, but
                // is the best we can do based on how nuget.org works
                searchTerm = $"tags:AzureSiteExtension title:\"{filter}\"";
            }
            return $"https://www.nuget.org/api/v2/Search?searchTerm=%27{searchTerm}%27&includePrerelease=true&semVerLevel=2.0.0";
        }

        // <summary>
        /// Query result by search term, always include pre-released
        /// </summary>
        public static async Task<IEnumerable<SiteExtensionInfo>> SearchLocalRepo(string siteExtensionRootPath, string searchTerm, SearchFilter filterOptions = null, int skip = 0, int take = 1000)
        {
            List<SiteExtensionInfo> extensions = new List<SiteExtensionInfo>();
            if (!Directory.Exists(siteExtensionRootPath))
            {
                return extensions;
            }

            // always include pre-release package
            if (filterOptions == null)
            {
                filterOptions = new SearchFilter();
            }

            filterOptions.IncludePrerelease = true; // keep the good old behavior

            List<string> installedPackages = new List<string>(Directory.EnumerateDirectories(siteExtensionRootPath));
            int countEntries = 0;
            foreach (string extDir in installedPackages)
            {
                string nupkgFile = Directory.GetFiles(extDir, "*.nupkg", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (nupkgFile == null)
                {
                    continue;
                }

                if (skip > 0)
                {
                    --skip;
                    continue;
                }

                string packageId = extDir.Substring(extDir.LastIndexOf("\\", StringComparison.OrdinalIgnoreCase) + 1);
                if (countEntries++ > take)
                {
                    break;
                }

                string content = null;
                using (ZipFile nupkgZipFile = OperationManager.Attempt(() => ZipFile.Read(nupkgFile), delayBeforeRetry: 1000, shouldRetry: ex => ex is IOException))
                {
                    ZipEntry entry = nupkgZipFile[string.Format("{0}.nuspec", packageId)];
                    if (entry != null)
                    {
                        using (var reader = new StreamReader(entry.OpenReader()))
                        {
                            content = await reader.ReadToEndAsync();
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"{packageId}.nuspec does not exist in {nupkgFile}!");
                    }
                }

                if (string.IsNullOrEmpty(content))
                {
                    throw new InvalidOperationException($"{packageId}.nuspec contains empty content!");
                }

                if (string.IsNullOrEmpty(searchTerm) || content.Contains(searchTerm) || content.IndexOf($"<id>{searchTerm}</id>", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    using (var reader = XmlReader.Create(new System.IO.StringReader(content)))
                    {
                        reader.ReadToDescendant("metadata");
                        var extInfo = new SiteExtensionInfo((XElement)XNode.ReadFrom(reader));
                        extensions.Add(extInfo);
                    }
                }
            }

            return extensions;
        }

        /// <summary>
        /// <para>Query source repository for a package base on given package id and version</para>
        /// <para>Will also query pre-release and unlisted packages</para>
        /// </summary>
        /// <param name="packageId">Package id, must not be null</param>
        /// <param name="version">Package version, must not be null</param>
        /// <returns>Package metadata</returns>
        public static async Task<SiteExtensionInfo> GetPackageByIdentity(string packageId, string version = null)
        {
            if (string.IsNullOrEmpty(version))
            {
                return (await PackageCacheInfo.GetPackagesFromNugetAPI()).FirstOrDefault(a => a.Id != null && a.Id == packageId);
            }
            else
            {
                string address = null;
                try
                {
                    JObject json = null;
                    using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = HttpClientTimeout })
                    {
                        address = $"https://azuresearch-usnc.nuget.org/query?q=tags:AzureSiteExtension%20packageid:{packageId}&prerelease=true&semVerLevel=2.0.0";
                        using (var response = await client.GetAsync(address))
                        {
                            response.EnsureSuccessStatusCode();
                            json = JObject.Parse(await response.Content.ReadAsStringAsync());
                        }

                        json = (JObject)json.Value<JArray>("data").FirstOrDefault();
                        if (json == null)
                        {
                            return null;
                        }

                        json = (JObject)json.Value<JArray>("versions").FirstOrDefault(j => j.Value<string>("version") == version);
                        if (json == null)
                        {
                            return null;
                        }

                        address = json.Value<string>("@id");
                        using (var response = await client.GetAsync(address))
                        {
                            response.EnsureSuccessStatusCode();
                            json = JObject.Parse(await response.Content.ReadAsStringAsync());
                        }

                        address = json.Value<string>("catalogEntry");
                        using (var response = await client.GetAsync(address))
                        {
                            response.EnsureSuccessStatusCode();
                            json = JObject.Parse(await response.Content.ReadAsStringAsync());
                        }

                        return new SiteExtensionInfo(json);
                    }
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrEmpty(address))
                    {
                        throw;
                    }

                    throw new InvalidOperationException($"Http request to {address} failed with {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Helper function to download package from given url, and place package (only 'content' folder from package) to given folder
        /// </summary>
        /// <param name="identity">Package identity</param>
        /// <param name="destinationFolder">Folder where we copy the package content (content folder only) to</param>
        /// <param name="pathToLocalCopyOfNupkg">File path where we copy the nudpk to</param>
        /// <returns></returns>
        public static async Task DownloadPackageToFolder(SiteExtensionInfo package, string destinationFolder, string pathToLocalCopyOfNupkg)
        {
            var packageId = package.Id;
            var packageVersion = package.Version;
            using (var client = new HttpClient { Timeout = HttpClientTimeout })
            {
                var uri = new Uri(!string.IsNullOrEmpty(package.PackageUri) ? package.PackageUri : $"https://www.nuget.org/api/v2/package/{packageId}/{packageVersion}");
                var response = await client.GetAsync(uri);

                using (Stream packageStream = await response.Content.ReadAsStreamAsync())
                {
                    using (ZipFile zipFile = ZipFile.Read(packageStream))
                    {
                        // we only care about stuff under "content" folder
                        int substringStartIndex = @"content/".Length;
                        IEnumerable<ZipEntry> contentEntries = zipFile.Entries.Where(e => e.FileName.StartsWith(@"content/", StringComparison.InvariantCultureIgnoreCase));
                        foreach (var entry in contentEntries)
                        {
                            string entryFileName = Uri.UnescapeDataString(entry.FileName);
                            string fullPath = Path.Combine(destinationFolder, entryFileName.Substring(substringStartIndex));

                            if (entry.IsDirectory)
                            {
                                FileSystemHelpers.EnsureDirectory(fullPath.Replace('/', '\\'));
                                continue;
                            }

                            FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(fullPath));
                            using (Stream writeStream = FileSystemHelpers.OpenWrite(fullPath))
                            {
                                // reset length of file stream
                                writeStream.SetLength(0);

                                // let the thread go with itself, so that once file finishes writing, doesn't need to request thread context from main thread
                                await entry.OpenReader().CopyToAsync(writeStream).ConfigureAwait(false);
                            }
                        }
                    }

                    // set position back to the head of stream
                    packageStream.Position = 0;

                    // save a copy of the nupkg at last
                    WriteStreamToFile(packageStream, pathToLocalCopyOfNupkg);
                }
            }
        }

        public static async Task UpdateLocalPackage(string siteExtentionsRootPath, SiteExtensionInfo package, string destinationFolder, string pathToLocalCopyOfNupkg, ITracer tracer)
        {
            var packageId = package.Id;
            var packageVersion = package.Version;
            tracer.Trace("Performing incremental package update for {0}", packageId);
            using (var client = new HttpClient { Timeout = HttpClientTimeout })
            {
                var uri = new Uri(!string.IsNullOrEmpty(package.PackageUri) ? package.PackageUri : $"https://www.nuget.org/api/v2/package/{packageId}/{packageVersion}");
                var response = await client.GetAsync(uri);
                using (Stream newPackageStream = await response.Content.ReadAsStreamAsync())
                {
                    // update file
                    var localPackage = (await FeedExtensionsV2.SearchLocalRepo(siteExtentionsRootPath, packageId)).FirstOrDefault();
                    if (localPackage == null)
                    {
                        throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "Package {0} not found from local repo.", packageId));
                    }

                    string nupkgFile = Directory.GetFiles(Path.Combine(siteExtentionsRootPath, packageId), "*.nupkg", SearchOption.TopDirectoryOnly).FirstOrDefault();

                    using (ZipFile oldPackageZip = ZipFile.Read(nupkgFile))
                    using (ZipFile newPackageZip = ZipFile.Read(newPackageStream))
                    {
                        // we only care about stuff under "content" folder
                        IEnumerable<ZipEntry> oldContentEntries = oldPackageZip.Entries.Where(e => e.FileName.StartsWith(@"content/", StringComparison.InvariantCultureIgnoreCase));
                        IEnumerable<ZipEntry> newContentEntries = newPackageZip.Entries.Where(e => e.FileName.StartsWith(@"content/", StringComparison.InvariantCultureIgnoreCase));
                        List<ZipEntry> filesNeedToUpdate = new List<ZipEntry>();
                        Dictionary<string, ZipEntry> indexedOldFiles = new Dictionary<string, ZipEntry>();
                        foreach (var item in oldContentEntries)
                        {
                            indexedOldFiles.Add(item.FileName.ToLowerInvariant(), item);
                        }

                        foreach (var newEntry in newContentEntries)
                        {
                            var fileName = newEntry.FileName.ToLowerInvariant();
                            if (indexedOldFiles.ContainsKey(fileName))
                            {
                                // file name existed, only update if file has been touched
                                ZipEntry oldEntry = indexedOldFiles[fileName];
                                if (oldEntry.LastModified != newEntry.LastModified)
                                {
                                    filesNeedToUpdate.Add(newEntry);
                                }

                                // remove from old index files buffer, the rest will be files that need to be deleted
                                indexedOldFiles.Remove(fileName);
                            }
                            else
                            {
                                // new files
                                filesNeedToUpdate.Add(newEntry);
                            }
                        }

                        int substringStartIndex = @"content/".Length;

                        foreach (var entry in filesNeedToUpdate)
                        {
                            string entryFileName = Uri.UnescapeDataString(entry.FileName);
                            string fullPath = Path.Combine(destinationFolder, entryFileName.Substring(substringStartIndex));

                            if (entry.IsDirectory)
                            {
                                using (tracer.Step("Ensure directory: {0}", fullPath))
                                {
                                    FileSystemHelpers.EnsureDirectory(fullPath.Replace('/', '\\'));
                                }

                                continue;
                            }

                            using (tracer.Step("Adding/Updating file: {0}", fullPath))
                            {
                                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(fullPath));
                                using (Stream writeStream = FileSystemHelpers.OpenWrite(fullPath))
                                {
                                    // reset length of file stream
                                    writeStream.SetLength(0);

                                    // let the thread go with itself, so that once file finishes writing, doesn't need to request thread context from main thread
                                    await entry.OpenReader().CopyToAsync(writeStream).ConfigureAwait(false);
                                }
                            }
                        }

                        foreach (var entry in indexedOldFiles.Values)
                        {
                            string entryFileName = Uri.UnescapeDataString(entry.FileName);
                            string fullPath = Path.Combine(destinationFolder, entryFileName.Substring(substringStartIndex));

                            if (entry.IsDirectory)
                            {
                                // in case the two zip file was created from different tool. some tool will include folder as seperate entry, some don`t.
                                // to be sure that folder is meant to be deleted, double check there is no files under it
                                var entryNameInLower = entryFileName.ToLower();
                                if (!string.Equals(destinationFolder, fullPath, StringComparison.OrdinalIgnoreCase)
                                    && newContentEntries.FirstOrDefault(e => e.FileName.ToLowerInvariant().StartsWith(entryNameInLower)) == null)
                                {
                                    using (tracer.Step("Deleting directory: {0}", fullPath))
                                    {
                                        FileSystemHelpers.DeleteDirectorySafe(fullPath);
                                    }
                                }
                                continue;
                            }

                            using (tracer.Step("Deleting file: {0}", fullPath))
                            {
                                FileSystemHelpers.DeleteFileSafe(fullPath);
                            }
                        }
                    }

                    // update nupkg
                    newPackageStream.Position = 0;
                    using (tracer.Step("Updating nupkg file."))
                    {
                        WriteStreamToFile(newPackageStream, pathToLocalCopyOfNupkg);
                        if (!packageVersion.Equals(localPackage.Version))
                        {
                            using (tracer.Step("New package has difference version {0} from old package {1}. Remove old nupkg file.", packageVersion, localPackage.Version))
                            {
                                // if version is difference, nupkg file name will be difference. will need to clean up the old one.
                                var oldNupkg = pathToLocalCopyOfNupkg.Replace(
                                    string.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", packageId, packageVersion),
                                    string.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", localPackage.Id, localPackage.Version));

                                FileSystemHelpers.DeleteFileSafe(oldNupkg);
                            }
                        }
                    }
                }
            }
        }

        private static void WriteStreamToFile(Stream stream, string filePath)
        {
            string packageFolderPath = Path.GetDirectoryName(filePath);
            FileSystemHelpers.EnsureDirectory(packageFolderPath);
            using (Stream writeStream = FileSystemHelpers.OpenWrite(filePath))
            {
                OperationManager.Attempt(() =>
                {
                    // reset length of file stream
                    writeStream.SetLength(0);

                    stream.CopyTo(writeStream);
                });
            }
        }

        public class PackageCacheInfo
        {
            private static ConcurrentDictionary<string, PackageCacheInfo> PackageCaches = new ConcurrentDictionary<string, PackageCacheInfo>(StringComparer.OrdinalIgnoreCase);

            public DateTime ExpiredUtc { get; set; }

            public List<SiteExtensionInfo> Extensions { get; set; }

            public static async Task<List<SiteExtensionInfo>> GetPackagesFromNugetAPI(string filter = null, string feedUrl = null)
            {
                var extensions = new List<SiteExtensionInfo>();

                var key = string.Format("{0}:{1}", filter, feedUrl);
                PackageCacheInfo info;
                if (PackageCaches.TryGetValue(key, out info) && DateTime.UtcNow < info.ExpiredUtc)
                {
                    return info.Extensions;
                }

                if (string.IsNullOrEmpty(feedUrl))
                {
                    feedUrl = FeedExtensionsV2.GetFeedUrl(filter);
                }

                using (var client = new HttpClient { Timeout = HttpClientTimeout })
                {
                    var response = await client.GetAsync(feedUrl);
                    var content = await response.Content.ReadAsStringAsync();
                    using (var reader = XmlReader.Create(new System.IO.StringReader(content)))
                    {
                        reader.ReadStartElement("feed");
                        while (reader.Read())
                        {
                            if (reader.Name == "entry" && reader.IsStartElement())
                            {
                                reader.ReadToDescendant("m:properties");
                                extensions.Add(new SiteExtensionInfo((XElement)XNode.ReadFrom(reader)));
                            }
                        }
                    }
                }

                info = new PackageCacheInfo
                {
                    ExpiredUtc = DateTime.UtcNow.AddMinutes(10),
                    Extensions = extensions
                };

                PackageCaches.AddOrUpdate(key, info, (k, i) => info);
                if (PackageCaches.Count > 10)
                {
                    var toRemove = PackageCaches.OrderBy(p => p.Value.ExpiredUtc).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(toRemove.Key))
                    {
                        PackageCaches.TryRemove(toRemove.Key, out _);
                    }
                }

                return info.Extensions;
            }
        }
    }
}
