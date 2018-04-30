using System;
using Newtonsoft.Json.Linq;

namespace Kudu.Services
{
    public class DropboxEntryInfo
    {
        public string Path { get; set; }

        public string Nonce { get; set; }

        public string Signature { get; set; }

        public bool IsDirectory { get; set; }

        public bool IsDeleted { get; set; }

        public string Modified
        {
            get;
            set;
        }

        public static DropboxEntryInfo ParseFrom(JObject json)
        {
            // This preserves casing
            var correctlyCasedPath = json.Value<string>("path_display");
            var deltaInfo = new DropboxEntryInfo
            {
                Path = correctlyCasedPath
            };

            var tag = json.Value<string>(".tag");
            if (tag == null || String.Equals(tag, "deleted", StringComparison.OrdinalIgnoreCase))
            {
                deltaInfo.IsDeleted = true;
            }
            else
            {
                deltaInfo.IsDirectory = String.Equals(tag, "folder", StringComparison.OrdinalIgnoreCase);
                deltaInfo.Modified = json.Value<string>("server_modified");
            }

            return deltaInfo;
        }
    }
}
