using System.Collections.Generic;

namespace Kudu.Core.SourceControl
{
    public interface IRepository
    {
        string CurrentId { get; }

        void Initialize();
        IEnumerable<Branch> GetBranches();
        IEnumerable<FileStatus> GetStatus();
        IEnumerable<ChangeSet> GetChanges();
        IEnumerable<ChangeSet> GetChanges(int index, int limit);
        ChangeSetDetail GetDetails(string id);
        ChangeSetDetail GetWorkingChanges();
        void AddFile(string path);
        void RevertFile(string path);
        ChangeSet Commit(string authorName, string message);
        void Update(string id);
        void Push();
    }
}
