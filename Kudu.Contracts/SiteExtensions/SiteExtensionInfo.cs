using System;
using System.Collections.Generic;
using NuGet;
using Newtonsoft.Json;

namespace Kudu.Contracts.SiteExtensions
{
    // This is equivalent to NuGet.IPackage
    public class SiteExtensionInfo
    {
        public SiteExtensionInfo()
        {
        }

        public SiteExtensionInfo(SiteExtensionInfo info)
        {
            Id = info.Id;
            Title = info.Title;
            Summary = info.Summary;
            Description = info.Description;
            Version = info.Version;
            ExtensionUrl = info.ExtensionUrl;
            ProjectUrl = info.ProjectUrl;
            IconUrl = info.IconUrl;
            LicenseUrl = info.LicenseUrl;
            Authors = info.Authors;
            PublishedDateTime = info.PublishedDateTime;
            IsLatestVersion = info.IsLatestVersion;
            DownloadCount = info.DownloadCount;
            LocalIsLatestVersion = info.LocalIsLatestVersion;
            LocalPath = info.LocalPath;
            InstalledDateTime = info.InstalledDateTime;
        }

        public SiteExtensionInfo(IPackage package)
        {
            Id = package.Id;
            Title = package.Title;
            Summary = package.Summary;
            Description = package.Description;
            Version = package.Version == null ? null : package.Version.ToString();
            ProjectUrl = package.ProjectUrl == null ? null : package.ProjectUrl.ToString();
            IconUrl = package.IconUrl == null ? null : package.IconUrl.ToString();
            LicenseUrl = package.LicenseUrl == null ? null : package.LicenseUrl.ToString();
            Authors = package.Authors;
            PublishedDateTime = package.Published;
            IsLatestVersion = package.IsLatestVersion;
            DownloadCount = package.DownloadCount;
        }

        [JsonProperty(PropertyName = "id")]
        public string Id
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "title")]
        public string Title
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "summary")]
        public string Summary
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "description")]
        public string Description
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "version")]
        public string Version
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "extension_url")]
        public string ExtensionUrl
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "project_url")]
        public string ProjectUrl
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "icon_url")]
        public string IconUrl
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "license_url")]
        public string LicenseUrl
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "authors")]
        public IEnumerable<string> Authors
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "published_date_time")]
        public DateTimeOffset? PublishedDateTime
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "is_latest_version")]
        public bool IsLatestVersion
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "download_count")]
        public int DownloadCount
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "local_is_latest_version")]
        public bool? LocalIsLatestVersion
        {
            get; 
            set; 
        }

        [JsonProperty(PropertyName = "local_path")]
        public string LocalPath
        {
            get;
            set;
        }

        [JsonProperty(PropertyName = "installed_date_time")]
        public DateTimeOffset? InstalledDateTime
        {
            get;
            set;
        }
    }
}
