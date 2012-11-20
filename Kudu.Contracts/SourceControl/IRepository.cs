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
        ChangeSet GetChangeSet(string id);
        ChangeSetDetail GetDetails(string id);
        ChangeSetDetail GetWorkingChanges();
        void AddFile(string path);
        void RevertFile(string path);
        ChangeSet Commit(string message, string authorName);
        void Update(string id);
        void Update();
        void Push();
        void FetchWithoutConflict(string remote, string remoteAlias, string branchName);

        /// <summary>
        /// Creates a new branch or resets an existing branch at the given starting poijnt. 
        /// </summary>
        /// <param name="branchName">The name of the branch</param>
        /// <param name="startPoint">The starting point of the branch.</param>
        void CreateOrResetBranch(string branchName, string startPoint);

        /// <summary>
        /// Rebases a branch against master
        /// </summary>
        void Rebase();

        /// <summary>
        /// Aborts an ongoing rebase operation.
        /// </summary>
        void RebaseAbort();

        /// <summary>
        /// Updates the HEAD of master to be that of the HEAD or another branch.
        /// </summary>
        /// <param name="source">The source branch used to update master.</param>
        void UpdateRef(string source);
    }
}
