using System.Collections.Generic;

namespace Kudu.Core {
    public interface IRepository {
        string CurrentId { get; }

        void Initialize();
        IEnumerable<FileStatus> GetStatus();
        IEnumerable<ChangeSet> GetChanges();
        ChangeSetDetail GetDetails(string id);
        ChangeSetDetail GetWorkingChanges();
        void AddFile(string path);
        void RemoveFile(string path);
        ChangeSet Commit(string authorName, string message);
        void Update(string id);
    }
}
