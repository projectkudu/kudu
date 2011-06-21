using System;
using LibGit2Sharp;

namespace Kudu.Core {
    public class ChangeSet {
        public ChangeSet(string id, string authorName, string message, DateTimeOffset timestamp) {
            Id = id;
            AuthorName = authorName;
            Message = message;
            Timestamp = timestamp;
        }

        public string Id {
            get;
            private set;
        }

        public string AuthorName {
            get;
            private set;
        }

        public string Message {
            get;
            private set;
        }

        public DateTimeOffset Timestamp {
            get;
            private set;
        }

        public override string ToString() {
            return String.Format("{0} {1} {2} {3}", Id, Timestamp, AuthorName, Message);
        }
    }
}
