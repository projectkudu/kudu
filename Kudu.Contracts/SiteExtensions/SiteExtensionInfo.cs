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

        public SemanticVersion Version
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

        // TODO, suwatch: to run an extension
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

    // This is temporary, we will ref NuGet SemVer.
    public class SemanticVersion 
    {
    }
}
