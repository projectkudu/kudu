using System.Collections.Generic;

namespace Kudu.Core.SourceControl
{
    public interface IRepository
    {
        string CurrentId { get; }

        string RepositoryPath { get; }

        RepositoryType RepositoryType { get; }

        /// <summary>
        /// Determines if a valid repository exists at that location.
        /// </summary>
        bool Exists { get; }

        /// <summary>
        /// Initialize a new repository using the given configuration.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
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

        /// <summary>
        /// Commits new, modified and deleted files to the repository.
        /// </summary>
        /// <returns>True if one or more files were added to a changeset</returns>
        bool Commit(string message, string authorName);
        void Update(string id);
        void Update();
        void Push();

        /// <summary>
        /// Attempts to fetch from the remote repository without resulting in any merge conflicts.
        /// </summary>
        void FetchWithoutConflict(string remote, string branchName);

        void Clean();

        /// <summary>
        /// Updates sub repositories \ sub modules linked to this repository
        /// </summary>
        void UpdateSubmodules();

        /// <summary>
        /// Removes any physical lock files added to the repository that might prevent source control operations.
        /// </summary>
        /// <remarks></remarks>
        void ClearLock();
        

        /// <summary>
        /// Creates a new branch or resets an existing branch at the given starting point. 
        /// </summary>
        /// <param name="branchName">The name of the branch</param>
        /// <param name="startPoint">The starting point of the branch.</param>
        void CreateOrResetBranch(string branchName, string startPoint);

        /// <summary>
        /// Rebases a branch against master
        /// </summary>
        /// <returns>Returns true if branch is up to date with respect to master or false if updates from master were applied.</returns>
        bool Rebase(string branchName);

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
