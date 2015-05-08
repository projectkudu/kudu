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
            return await searchResource.Search(searchTerm, filterOptions, skip, take, CancellationToken.None);
        }

        /// <summary>
        /// Query source repository for latest package base given package id
        /// </summary>
        public static async Task<UIPackageMetadata> GetLatestPackageById(this SourceRepository srcRepo, string packageId, bool includePrerelease = true, bool includeUnlisted = false)
        {
            UIPackageMetadata latestPackage = null;
            var metadataResource = await srcRepo.GetResourceAndValidateAsync<UIMetadataResource>();
            IEnumerable<UIPackageMetadata> packages = await metadataResource.GetMetadata(packageId, includePrerelease, includeUnlisted, token: CancellationToken.None);
            foreach (var p in packages)
            {
                if (latestPackage == null ||
                    latestPackage.Identity.Version < p.Identity.Version)
                {
                    latestPackage = p;
                }
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
            return await metadataResource.GetMetadata(identity, CancellationToken.None);
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
                        string fullPath = Path.Combine(destinationFolder, entry.FileName.Substring(substringStartIndex));
                        FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(fullPath));
                        using (Stream writeStream = FileSystemHelpers.OpenWrite(fullPath))
                        {
                            // let the thread go with itself, so that once file finsihed writing, doesn`t need to request thread context from main thread
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
                var localPackage = await localRepo.GetLatestPackageById(identity.Id);
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
                        string fullPath = Path.Combine(destinationFolder, entry.FileName.Substring(substringStartIndex));
                        using (tracer.Step("Adding/Updating {0}", fullPath))
                        {
                            FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(fullPath));
                            using (Stream writeStream = FileSystemHelpers.OpenWrite(fullPath))
                            {
                                // let the thread go with itself, so that once file finsihed writing, doesn`t need to request thread context from main thread
                                await entry.OpenReader().CopyToAsync(writeStream).ConfigureAwait(false);
                            }
                        }
                    }

                    foreach (var entry in indexedOldFiles.Values)
                    {
                        string fullPath = Path.Combine(destinationFolder, entry.FileName.Substring(substringStartIndex));
                        using (tracer.Step("Deleting {0}", fullPath))
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

        private static async Task<T> GetResourceAndValidateAsync<T>(this SourceRepository srcRepo) where T : class, INuGetResource
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
                OperationManager.Attempt(() => stream.CopyTo(writeStream));
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
    }
}
