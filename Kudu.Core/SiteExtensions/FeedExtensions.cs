using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ionic.Zip;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using NuGet.Client;
using NuGet.Client.VisualStudio;
using NuGet.PackagingCore;
using NuGet.Versioning;

namespace Kudu.Core.SiteExtensions
{
    /// <summary>
    /// Helper function to query feed
    /// </summary>
    public static class FeedExtensions
    {
        /// <summary>
        /// Query result by search term, always include pre-released
        /// </summary>
        public static async Task<IEnumerable<UIPackageMetadata>> Search(this SourceRepository srcRepo, string searchTerm, SearchFilter filterOptions = null, int skip = 0, int take = 1000)
        {
            // always include pre-release package
            if (filterOptions == null)
            {
                filterOptions = new SearchFilter();
            }

            filterOptions.IncludePrerelease = true; // keep the good old behavior
            var searchResource = await srcRepo.GetResourceAndValidateAsync<SearchLatestResource>();

            // When using nuget.org, we only look at packages that have our tag. Later, we should switch to filtering
            // by PackageType once nuget.org starts supporting that
            if (IsNuGetRepo(srcRepo.PackageSource.Source))
            {
                if (String.IsNullOrWhiteSpace(searchTerm))
                {
                    // No user provided search string: just search by tag
                    searchTerm = "tags:AzureSiteExtension";
                }
                else if (searchTerm.Contains(":"))
                {
                    // User provided complex query with fields: add it as is to the tag field query
                    searchTerm = $"tags:AzureSiteExtension {searchTerm}";
                }
                else
                {
                    // User provided simple string: treat it as a title. This is not ideal behavior, but
                    // is the best we can do based on how nuget.org works
                    searchTerm = $"tags:AzureSiteExtension title:\"{searchTerm}\"";
                }
            }

            return await searchResource.Search(searchTerm, filterOptions, skip, take, CancellationToken.None);
        }

        /// <summary>
        /// Query source repository for latest package base given package id
        /// </summary>
        internal static async Task<UIPackageMetadata> GetLatestPackageByIdFromSrcRepo(this SourceRepository srcRepo, string packageId)
        {
            // 7 references none of which uses bool includePrerelease = true, bool includeUnlisted = false
            var metadataResource = await srcRepo.GetResourceAndValidateAsync<UIMetadataResource>();
            return await metadataResource.GetLatestPackageByIdFromMetaRes(packageId,
                explicitTag: IsNuGetRepo(srcRepo.PackageSource.Source));
        }

        // can be called concurrently if metaDataResource is provided
        internal static async Task<UIPackageMetadata> GetLatestPackageByIdFromMetaRes(this UIMetadataResource metadataResource, string packageId, bool includePrerelease = true, bool includeUnlisted = false, bool explicitTag = false)
        {
            UIPackageMetadata latestPackage = null;
            IEnumerable<UIPackageMetadata> packages = await metadataResource.GetMetadata(packageId, includePrerelease, includeUnlisted, token: CancellationToken.None);

            // When using nuget.org, we only look at packages that have our tag.
            if (explicitTag)
            {
                packages = packages.Where(item => item.Tags.IndexOf("azuresiteextension", StringComparison.OrdinalIgnoreCase) >= 0);
            }

            foreach (var p in packages)
            {
                if (latestPackage == null ||
                    latestPackage.Identity.Version < p.Identity.Version)
                {
                    latestPackage = p;
                }
            }

            // If we couldn't find any listed version, fall back to looking for unlisted versions, to avoid failing completely.
            // Reasoning is that if all the versions have been unlisted, it should still be possible to install it by
            // explicit id, even without specifying a version
            if (latestPackage == null && !includeUnlisted)
            {
                latestPackage = await GetLatestPackageByIdFromMetaRes(metadataResource, packageId, includePrerelease, includeUnlisted: true, explicitTag: explicitTag);
            }

            return latestPackage;
        }

        /// <summary>
        /// <para>Query source repository for a package base on given package id and version</para>
        /// <para>Will also query pre-release and unlisted packages</para>
        /// </summary>
        /// <param name="packageId">Package id, must not be null</param>
        /// <param name="version">Package version, must not be null</param>
        /// <returns>Package metadata</returns>
        public static async Task<UIPackageMetadata> GetPackageByIdentity(this SourceRepository srcRepo, string packageId, string version)
        {
            var metadataResource = await srcRepo.GetResourceAndValidateAsync<UIMetadataResource>();
            NuGetVersion expectedVersion = NuGetVersion.Parse(version);
            var identity = new PackageIdentity(packageId, expectedVersion);
            UIPackageMetadata ret = await metadataResource.GetMetadata(identity, CancellationToken.None);

            // When using nuget.org, we only look at packages that have our tag.
            if (ret != null &&
                IsNuGetRepo(srcRepo.PackageSource.Source) &&
                (ret.Tags.IndexOf("azuresiteextension", StringComparison.OrdinalIgnoreCase) < 0))
            {
                ret = null;
            }
            return ret;
        }

        /// <summary>
        /// Helper function to download package from given url, and place package (only 'content' folder from package) to given folder
        /// </summary>
        /// <param name="identity">Package identity</param>
        /// <param name="destinationFolder">Folder where we copy the package content (content folder only) to</param>
        /// <param name="pathToLocalCopyOfNupkg">File path where we copy the nudpk to</param>
        /// <returns></returns>
        public static async Task DownloadPackageToFolder(this SourceRepository srcRepo, PackageIdentity identity, string destinationFolder, string pathToLocalCopyOfNupkg)
        {
            var downloadResource = await srcRepo.GetResourceAndValidateAsync<DownloadResource>();
            using (Stream packageStream = await srcRepo.GetPackageStream(identity))
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

        public static async Task UpdateLocalPackage(this SourceRepository srcRepo, SourceRepository localRepo, PackageIdentity identity, string destinationFolder, string pathToLocalCopyOfNupkg, ITracer tracer)
        {
            tracer.Trace("Performing incremental package update for {0}", identity.Id);
            using (Stream newPackageStream = await srcRepo.GetPackageStream(identity))
            {
                // update file
                var localPackage = await localRepo.GetLatestPackageByIdFromSrcRepo(identity.Id);
                if (localPackage == null)
                {
                    throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "Package {0} not found from local repo.", identity.Id));
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
                            // to be sure that foder is meant to be deleted, double check there is no files under it
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
                    if (!identity.Version.Equals(localPackage.Identity.Version))
                    {
                        using (tracer.Step("New package has difference version {0} from old package {1}. Remove old nupkg file.", identity.Version, localPackage.Identity.Version))
                        {
                            // if version is difference, nupkg file name will be difference. will need to clean up the old one.
                            var oldNupkg = pathToLocalCopyOfNupkg.Replace(
                                string.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", identity.Id, identity.Version.ToNormalizedString()),
                                string.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", localPackage.Identity.Id, localPackage.Identity.Version.ToNormalizedString()));

                            FileSystemHelpers.DeleteFileSafe(oldNupkg);
                        }
                    }
                }
            }
        }

        internal static async Task<T> GetResourceAndValidateAsync<T>(this SourceRepository srcRepo) where T : class, INuGetResource
        {
            var resource = await srcRepo.GetResourceAsync<T>();
            if (resource == null)
            {
                // if endpoint is invalid, NuGet client would return us a null resource
                // throw a specific error, so that caller will have a clear understanding what is happening

                string feed = srcRepo.PackageSource != null ? srcRepo.PackageSource.Source : string.Empty;
                throw new InvalidEndpointException(string.Format("Invalid remote feed url: {0}", feed));
            }

            return resource;
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

        internal static bool IsNuGetRepo(string repoUrl)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                repoUrl,
                @"https://.*\.nuget\.org/.*",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
