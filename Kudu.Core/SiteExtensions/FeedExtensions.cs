using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ionic.Zip;
using Kudu.Contracts.SiteExtensions;
using Kudu.Core.Infrastructure;
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
        /// <param name="pathToLocalCopyOfNudpk">File path where we copy the nudpk to</param>
        /// <returns></returns>
        public static async Task DownloadPackageToFolder(this SourceRepository srcRepo, PackageIdentity identity, string destinationFolder, string pathToLocalCopyOfNudpk = null)
        {
            var downloadResource = await srcRepo.GetResourceAndValidateAsync<DownloadResource>();
            using (Stream sourceStream = await downloadResource.GetStream(identity, CancellationToken.None))
            {
                if (sourceStream == null)
                {
                    // package not exist from feed
                    throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "Package {0} - {1} not found when try to download.", identity.Id, identity.Version.ToNormalizedString()));
                }

                Stream packageStream = sourceStream;
                try
                {
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

                    if (!string.IsNullOrWhiteSpace(pathToLocalCopyOfNudpk))
                    {
                        string packageFolderPath = Path.GetDirectoryName(pathToLocalCopyOfNudpk);
                        FileSystemHelpers.CreateDirectory(packageFolderPath);
                        using (Stream writeStream = FileSystemHelpers.OpenWrite(pathToLocalCopyOfNudpk))
                        {
                            OperationManager.Attempt(() => packageStream.CopyTo(writeStream));
                        }

                        // set position back to the head of stream
                        packageStream.Position = 0;
                    }

                    using (ZipFile zipFile = ZipFile.Read(packageStream))
                    {
                        // we only care about stuff under "content" folder
                        int substringStartIndex = @"content/".Length;
                        IEnumerable<ZipEntry> contentEntries = zipFile.Entries.Where(e => e.FileName.StartsWith(@"content/", StringComparison.InvariantCultureIgnoreCase));
                        foreach (var entry in contentEntries)
                        {
                            string fullPath = Path.Combine(destinationFolder, entry.FileName.Substring(substringStartIndex));
                            FileSystemHelpers.CreateDirectory(Path.GetDirectoryName(fullPath));
                            using (Stream writeStream = FileSystemHelpers.OpenWrite(fullPath))
                            {
                                // let the thread go with itself, so that once file finsihed writing, doesn`t need to request thread context from main thread
                                await entry.OpenReader().CopyToAsync(writeStream).ConfigureAwait(false);
                            }
                        }
                    }
                }
                finally
                {
                    if (packageStream != null && !sourceStream.CanSeek)
                    {
                        // in this case, we created a copy of the source stream in memoery, need to manually dispose
                        packageStream.Dispose();
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
    }
}
