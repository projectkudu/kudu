using System;

namespace Kudu.Contracts.Dropbox
{
    public class DropboxDeltaInfo
    {
        public string Path { get; set; }

        public string Nonce { get; set; }

        public string Signature { get; set; }

        public bool IsDirectory { get; set; }

        public bool IsDeleted { get; set; }

        public string Modified { get; set; }
    }
}
