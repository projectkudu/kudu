using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kudu.Core {
    public interface IRepository {
        string CurrentId { get; }

        void Initialize();
        IEnumerable<FileStatus> GetStatus();
        IEnumerable<ChangeSet> GetChanges();
        ChangeSetDetail GetDetails(string id);
        void AddFile(string path);
        void RemoveFile(string path);
        ChangeSet Commit(string authorName, string message);
        void Update(string id);
    }
}
