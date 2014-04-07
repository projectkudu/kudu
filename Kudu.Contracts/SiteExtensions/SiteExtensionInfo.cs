using System;
using System.Collections.Generic;
using NuGet;

namespace Kudu.Contracts.SiteExtensions
{
    // This is equivalent to NuGet.IPackage
    public class SiteExtensionInfo
    {
        public SiteExtensionInfo()
        {
        }

        public SiteExtensionInfo(IPackage package)
        {
            Id = package.Id;
            Title = package.Title;
            Summary = package.Summary;
            Description = package.Description;
            Version = package.Version.ToString();
            ProjectUrl = package.ProjectUrl == null ? null : package.ProjectUrl.ToString();
            IconUrl = package.IconUrl == null ? null : package.IconUrl.ToString();
            LicenseUrl = package.LicenseUrl == null ? null : package.LicenseUrl.ToString();
            Authors = package.Authors;
            PublishedDateTime = package.Published;
            IsLatestVersion = package.IsLatestVersion;
            DownloadCount = package.DownloadCount;
        }

        public string Id
        {
            get;
            set;
        }

        public string Title
        {
            get;
            set;
        }

        public string Summary
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public string Version
        {
            get;
            set;
        }

        public string SiteUrl
        {
            get;
            set;
        }

        public string ProjectUrl
        {
            get;
            set;
        }

        public string IconUrl
        {
            get;
            set;
        }

        public string LicenseUrl
        {
            get;
            set;
        }

        public IEnumerable<string> Authors
        {
            get;
            set;
        }

        public DateTimeOffset? PublishedDateTime
        {
            get;
            set;
        }

        public bool IsLatestVersion
        {
            get;
            set;
        }

        public int DownloadCount
        {
            get;
            set;
        }

        public bool? LocalIsLatestVersion
        {
            get; 
            set; 
        }

        public string LocalPath
        {
            get;
            set;
        }

        public DateTimeOffset? InstalledDateTime
        {
            get;
            set;
        }
    }
}
