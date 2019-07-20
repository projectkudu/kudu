using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Ionic.Zip;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using NuGet.Client;
using NuGet.PackagingCore;

namespace Kudu.Core.SiteExtensions
{
    public static class FeedExtensionsV2
    {
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
        public static IEnumerable<SiteExtensionInfo> SearchLocalRepo(string siteExtensionRootPath, string searchTerm, SearchFilter filterOptions = null, int skip = 0, int take = 1000)
        {
            List<SiteExtensionInfo> extensions = new List<SiteExtensionInfo>();
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


                string nuspecFile = Directory.GetFiles(extDir, "*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (nuspecFile == null)
                {
                    using (ZipFile nupkgZipFile = ZipFile.Read(nupkgFile))
                    {
                        ZipEntry entry = nupkgZipFile[string.Format("{0}.nuspec", packageId)];
                        if (entry != null)
                        {
                            entry.Extract(extDir);
                        }
                        else
                        {
                            if (nuspecFile == null)
                            {
                                throw new InvalidOperationException($"{packageId}.nuspec does not exist in {nupkgFile}!");
                            }
                        }
                    }

                    nuspecFile = Directory.GetFiles(extDir, "*.nuspec", SearchOption.TopDirectoryOnly).First();
                }

                string data = FileSystemHelpers.ReadAllText(nuspecFile);
                if (string.IsNullOrEmpty(searchTerm) || data.Contains(searchTerm))
                {
                    using (XmlReader reader = XmlReader.Create(nuspecFile))
                    {
                        reader.ReadToDescendant("metadata");
                        var extInfo = new SiteExtensionInfo((XElement)XNode.ReadFrom(reader));
                        extensions.Add(extInfo);
                    }
                }
            }

            return extensions;
        }

        public static List<SiteExtensionInfo> GetPackagesFromNugetAPI(string filter = null, string feedUrl = null)
        {
            List<SiteExtensionInfo> extensions = new List<SiteExtensionInfo>();

            if (feedUrl == null)
            {
                feedUrl = FeedExtensionsV2.GetFeedUrl(filter);
            }

            using (XmlReader reader = XmlReader.Create(feedUrl))
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
            return extensions;
        }

        /// <summary>
        /// Query source repository for latest package base given package id
        /// </summary>
        internal static SiteExtensionInfo GetLatestPackageByIdFromSrcRepo(string packageId)
        {
            return GetPackagesFromNugetAPI().First(a => a.Id != null && a.Id == packageId);
        }

        /// <summary>
        /// <para>Query source repository for a package base on given package id and version</para>
        /// <para>Will also query pre-release and unlisted packages</para>
        /// </summary>
        /// <param name="packageId">Package id, must not be null</param>
        /// <param name="version">Package version, must not be null</param>
        /// <returns>Package metadata</returns>
        public static SiteExtensionInfo GetPackageByIdentity(string packageId, string version)
        {
            return GetPackagesFromNugetAPI().First(a => a.Id != null && a.Version != null && a.Id == packageId && a.Version == version);
        }

        /// <summary>
        /// Helper function to download package from given url, and place package (only 'content' folder from package) to given folder
        /// </summary>
        /// <param name="identity">Package identity</param>
        /// <param name="destinationFolder">Folder where we copy the package content (content folder only) to</param>
        /// <param name="pathToLocalCopyOfNupkg">File path where we copy the nudpk to</param>
        /// <returns></returns>
        public static async Task DownloadPackageToFolder(string packageId, string packageVersion, string destinationFolder, string pathToLocalCopyOfNupkg)
        {
            using (var client = new HttpClient())
            {
                var uri = new Uri(String.Format("https://www.nuget.org/api/v2/package/{0}/{1}", packageId, packageVersion));

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

        public static async Task UpdateLocalPackage(SourceRepository localRepo, string packageId, string packageVersion, string destinationFolder, string pathToLocalCopyOfNupkg, ITracer tracer)
        {
            tracer.Trace("Performing incremental package update for {0}", packageId);
            using (var client = new HttpClient())
            {
                var uri = new Uri(String.Format("https://www.nuget.org/api/v2/package/{0}/{1}", packageId, packageVersion));
                var response = await client.GetAsync(uri);
                using (Stream newPackageStream = await response.Content.ReadAsStreamAsync())
                {
                    // update file
                    var localPackage = await localRepo.GetLatestPackageByIdFromSrcRepo(packageId);
                    if (localPackage == null)
                    {
                        throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "Package {0} not found from local repo.", packageId));
                    }
                    using (Stream oldPackageStream = await localRepo.GetPackageStream(localPackage.Identity))
                    using (ZipFile oldPackageZip = ZipFile.Read(oldPackageStream))
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
                        if (!packageVersion.Equals(localPackage.Identity.Version))
                        {
                            using (tracer.Step("New package has difference version {0} from old package {1}. Remove old nupkg file.", packageVersion, localPackage.Identity.Version))
                            {
                                // if version is difference, nupkg file name will be difference. will need to clean up the old one.
                                var oldNupkg = pathToLocalCopyOfNupkg.Replace(
                                    string.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", packageId, packageVersion),
                                    string.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", localPackage.Identity.Id, localPackage.Identity.Version.ToNormalizedString()));

                                FileSystemHelpers.DeleteFileSafe(oldNupkg);
                            }
                        }
                    }
                }
            }
        }

        private static async Task<Stream> GetPackageStream(this SourceRepository srcRepo, PackageIdentity identity)
        {
            var downloadResource = await srcRepo.GetResourceAsync<DownloadResource>();
            Stream sourceStream = null;
            Stream packageStream = null;

            try
            {
                sourceStream = await downloadResource.GetStream(identity, CancellationToken.None);
                if (sourceStream == null)
                {
                    // package not exist from feed
                    throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "Package {0} - {1} not found when try to download.", identity.Id, identity.Version.ToNormalizedString()));
                }

                packageStream = sourceStream;
                if (!sourceStream.CanSeek)
                {
                    // V3 DownloadResource.GetStream does not support seek operations.
                    // https://github.com/NuGet/NuGet.Protocol/issues/15

                    MemoryStream memStream = new MemoryStream();

                    try
                    {
                        byte[] buffer = new byte[2048];

                        int bytesRead = 0;
                        do
                        {
                            bytesRead = sourceStream.Read(buffer, 0, buffer.Length);
                            memStream.Write(buffer, 0, bytesRead);
                        } while (bytesRead != 0);

                        await memStream.FlushAsync();
                        memStream.Position = 0;

                        packageStream = memStream;
                    }
                    catch
                    {
                        memStream.Dispose();
                        throw;
                    }
                }

                return packageStream;
            }
            catch
            {
                if (packageStream != null && packageStream != sourceStream)
                {
                    packageStream.Dispose();
                }

                if (sourceStream != null)
                {
                    sourceStream.Dispose();
                }

                throw;
            }
            finally
            {
                if (packageStream != null && sourceStream != null && !sourceStream.CanSeek)
                {
                    // packageStream is a copy of sourceStream, dispose sourceStream
                    sourceStream.Dispose();
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
    }
}
