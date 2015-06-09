using System;
using System.Collections.Generic;

namespace Kudu.Services.FetchHelpers
{
    /// <summary>
    /// Model object to deserialize reponse from OneDrive API
    /// </summary>
    public static class OneDriveModel
    {
        public class OneDriveItem
        {
            public string id { get; set; }
            public string name { get; set; }
            public int size { get; set; }
            public string cTag { get; set; }
            public string eTag { get; set; }
            public DateTime createdDateTime { get; set; }
            public DateTime lastModifiedDateTime { get; set; }
            public OneDriveFolderFacet folder { get; set; }
            public OneDriveFileFacet file { get; set; }
            public Dictionary<string, object> deleted { get; set; }
            public OneDriveParentReference parentReference { get; set; }
        }

        public class OneDriveParentReference
        {
            public string id { get; set; }
            public string driveId { get; set; }
        }

        public class OneDriveFolderFacet
        {
            public int childCount { get; set; }
        }

        public class OneDriveFileFacet
        {
            public OneDriveFileHashes hashes { get; set; }
            public string mimeType { get; set; }
        }

        public class OneDriveFileHashes
        {
            public string crc32Hash { get; set; }
            public string sha1Hash { get; set; }
        }

        public class OneDriveChange
        {
            public string Path { get; set; }
            public bool IsFile { get; set; }
            public bool IsDeleted { get; set; }
            public string ContentUri { get; set; }
            public DateTime LastModifiedUtc { get; set; }
        }

        public class OneDriveChangeCollection : List<OneDriveChange>
        {
            public string Cursor { get; set; }
        }

        public class ItemInfo
        {
            public string name { get; set; }
            public string parentId { get; set; }
        }

        public class OneDriveItemCollection
        {
            public List<OneDriveItem> value { get; set; }
        }
    }
}
