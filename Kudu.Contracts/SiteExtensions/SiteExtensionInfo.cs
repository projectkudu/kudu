using System;

namespace Kudu.Contracts.SiteExtensions
{
    // This is equivalent to NuGet.IPackage
    public class SiteExtensionInfo
    {
        public string Id
        {
            get;
            set;
        }

        public string Name 
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

        public SiteExtensionInfo Update
        {
            get;
            set;
        }

        public Uri HRef
        {
            get;
            set;
        }

        public string Author
        {
            get;
            set;
        }

        public DateTime PublishedDateTime
        {
            get;
            set;
        }

        public Uri LicenseUrl
        {
            get;
            set;
        }

        public string AppPath
        {
            get;
            set;
        }

        public DateTime InstalledDateTime
        {
            get;
            set;
        }
    }
}
