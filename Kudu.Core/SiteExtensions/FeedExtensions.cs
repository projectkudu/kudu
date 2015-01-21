using Ionic.Zip;
using Kudu.Core.Infrastructure;
using NuGet.Client;
using NuGet.Client.VisualStudio;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        public static async Task<IEnumerable<UISearchMetadata>> Search(this SourceRepository srcRepo, string searchTerm, SearchFilter filterOptions = null, int skip = 0, int take = 1000)
        {
            // always include pre-release package
            if (filterOptions == null)
            {
                filterOptions = new SearchFilter();
            }

            filterOptions.IncludePrerelease = true; // keep the good old behavior
            var searchResource = await srcRepo.GetResourceAsync<UISearchResource>();
            return await searchResource.Search(searchTerm, filterOptions, skip, take, CancellationToken.None);
        }

        /// <summary>
        /// Query source repository for latest package base given package id
        /// </summary>
        public static async Task<UIPackageMetadata> GetLatestPackageById(this SourceRepository srcRepo, string packageId)
        {
            UIPackageMetadata latestPackage = null;
            var metadataResource = await srcRepo.GetResourceAsync<UIMetadataResource>();
            IEnumerable<UIPackageMetadata> packages = await metadataResource.GetMetadata(packageId, true, true, CancellationToken.None);
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
        /// Query source repository for a package base given package id and version
        /// </summary>
        public static async Task<UIPackageMetadata> GetPackageByIdentity(this SourceRepository srcRepo, string packageId, string version)
        {
            var metadataResource = await srcRepo.GetResourceAsync<UIMetadataResource>();
            IEnumerable<UIPackageMetadata> packages = await metadataResource.GetMetadata(packageId, true, true, CancellationToken.None);
            NuGetVersion expectedVersion = NuGetVersion.Parse(version);
            return packages.FirstOrDefault((p) =>
            {
                return p.Identity.Version.Equals(expectedVersion);
            });
        }

        /// <summary>
        /// Helper function to download package from given url, and place package (only 'content' folder from package) to given folder
        /// </summary>
        public static async Task DownloadPackageToFolder(this SourceRepository srcRepo, PackageIdentity identity, string destinationFolder)
        {
            var downloadResource = await srcRepo.GetResourceAsync<DownloadResource>();
            using (Stream sourceStream = await downloadResource.GetStream(identity, CancellationToken.None))
            {
                Stream packageStream = sourceStream;
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
        }
    }
}
