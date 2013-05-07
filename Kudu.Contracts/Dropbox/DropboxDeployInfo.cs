using System;
using System.Collections.Generic;

namespace Kudu.Contracts.Dropbox
{
    public class DropboxDeployInfo
    {
        public string ConsumerKey { get; set; }

        public string SignatureMethod { get; set; }

        public string TimeStamp { get; set; }

        public string OAuthVersion { get; set; }

        public string Token { get; set; }

        public string OldCursor { get; set; }

        public string NewCursor { get; set; }

        public string Path { get; set; }

        public string UserName { get; set; }

        public string Email { get; set; }

        public ICollection<DropboxDeltaInfo> Deltas { get; set; }
    }
}
