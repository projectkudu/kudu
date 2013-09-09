using System.Collections.Generic;

namespace Kudu.Services
{
    public class DropboxDeployInfo
    {
        public DropboxDeployInfo()
        {
            Deltas = new List<DropboxEntryInfo>();
        }

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

        public List<DropboxEntryInfo> Deltas { get; private set; }
    }
}
