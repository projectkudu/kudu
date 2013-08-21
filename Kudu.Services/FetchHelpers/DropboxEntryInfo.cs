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

        public static DropboxEntryInfo ParseFrom(JArray json)
        {
            var deltaInfo = new DropboxEntryInfo
            {
                Path = (string)json[0]
            };

            JObject metadata = json[1] as JObject;
            if (metadata != null)
            {
                string correctlyCasedPath = metadata.Value<string>("path");
                if (!String.IsNullOrEmpty(correctlyCasedPath))
                {
                    // This preserves casing
                    deltaInfo.Path = correctlyCasedPath;
                }

                deltaInfo.IsDirectory = metadata.Value<bool>("is_dir");
                deltaInfo.IsDeleted = String.IsNullOrEmpty(correctlyCasedPath) || metadata.Value<bool>("is_deleted");

                if (!deltaInfo.IsDirectory && !deltaInfo.IsDeleted)
                {
                    deltaInfo.Modified = (string)metadata["modified"];
                }
            }
            else
            {
                deltaInfo.IsDeleted = true;
            }

            return deltaInfo;
        }
    }
}
