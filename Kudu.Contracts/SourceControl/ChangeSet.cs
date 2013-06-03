using System;

namespace Kudu.Core.SourceControl
{
    public class ChangeSet
    {
        public ChangeSet(string id, string authorName, string authorEmail, string message, DateTimeOffset timestamp)
        {
            Id = id;
            AuthorName = authorName;
            AuthorEmail = authorEmail;
            Message = message;
            Timestamp = timestamp;
        }

        public string Id
        {
            get;
            private set;
        }

        public string AuthorName
        {
            get;
            private set;
        }

        public string AuthorEmail
        {
            get;
            private set;
        }

        public string Message
        {
            get;
            private set;
        }

        public DateTimeOffset Timestamp
        {
            get;
            private set;
        }

        public bool IsTemporary
        {
            get;
            set;
        }

        public bool IsReadOnly
        {
            get;
            set;
        }

        public override string ToString()
        {
            return String.Format("{0} {1} {2} {3}", Id, Timestamp, AuthorName, Message);
        }
    }
}
