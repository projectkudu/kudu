using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl
{
    /// <summary>
    /// IRepository implementation not actually based on a repository. 
    /// Most methods are either no-op or not supported, but it knows how to preserve 
    /// commit information across one http request.
    /// </summary>
    public class NullRepository : IRepository
    {
        // for null repo, good enough for last known changeset
        private static ChangeSet _latestChangeSet;

        private readonly string _path;
        private readonly ITraceFactory _traceFactory;
        private readonly bool _doBuildDuringDeploymentByDefault;
        private readonly bool _deleteDirectoryAfterBuild;

        public NullRepository(string path, ITraceFactory traceFactory)
            :this(path, traceFactory, doBuildDuringDeploymentByDefault: true, deleteDirectoryAfterBuild: false)
        {
        }

        public NullRepository(
            string path,
            ITraceFactory traceFactory,
            bool doBuildDuringDeploymentByDefault,
            bool deleteDirectoryAfterBuild)
        {
            _path = path;
            _traceFactory = traceFactory;
            _doBuildDuringDeploymentByDefault = doBuildDuringDeploymentByDefault;
            _deleteDirectoryAfterBuild = deleteDirectoryAfterBuild;
        }

        public string CurrentId
        {
            get
            {
                var changeSet = _latestChangeSet;
                return changeSet == null ? null : changeSet.Id;
            }
        }

        public string RepositoryPath
        {
            get { return _path; }
        }

        public RepositoryType RepositoryType
        {
            get { return RepositoryType.None; }
        }

        public bool Exists
        {
            get { return true; }
        }

        public bool DoBuildDuringDeploymentByDefault
        {
            get { return _doBuildDuringDeploymentByDefault; }
        }

        public void Initialize()
        {
            // no-op
        }

        public ChangeSet GetChangeSet(string id)
        {
            var changeSet = _latestChangeSet;
            if (changeSet != null)
            {
                if (id == changeSet.Id || id == "master" || id == "HEAD")
                {
                    return changeSet;
                }
            }

            throw new InvalidOperationException();
        }

        public void AddFile(string path)
        {
            // no-op
        }

        public bool Commit(string message, string authorName, string emailAddress)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("None repository commit"))
            {
                var changeSet = new ChangeSet(Guid.NewGuid().ToString("N"),
                                              !string.IsNullOrEmpty(authorName) ? authorName : Resources.Deployment_UnknownValue,
                                              !string.IsNullOrEmpty(emailAddress) ? emailAddress : Resources.Deployment_UnknownValue,
                                              message ?? Resources.Deployment_UnknownValue,
                                              DateTimeOffset.UtcNow);

                // Does not support rollback
                changeSet.IsReadOnly = true;

                // Only maintain latest changeSet
                _latestChangeSet = changeSet;

                tracer.Trace("Commit id: " + changeSet.Id, new Dictionary<string, string>
                {
                    { "Message", changeSet.Message },
                    { "AuthorName", changeSet.AuthorName },
                    { "AuthorEmail", changeSet.AuthorEmail }
                });
            }

            return true;
        }

        public void Update(string id)
        {
            // no-op
        }

        public void Update()
        {
            // no-op
        }

        public void Push()
        {
            // no-op
        }

        public void FetchWithoutConflict(string remote, string branchName)
        {
            throw new NotSupportedException();
        }

        public void Clean()
        {
            _latestChangeSet = null;
        }

        public void UpdateSubmodules()
        {
            // no-op
        }

        public void ClearLock()
        {
            // no-op
        }

        public void CreateOrResetBranch(string branchName, string startPoint)
        {
            throw new NotSupportedException();
        }

        public bool Rebase(string branchName)
        {
            throw new NotSupportedException();
        }

        public void RebaseAbort()
        {
            // no-op
        }

        public IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList)
        {
            return FileSystemHelpers.ListFiles(path, searchOption, lookupList);
        }

        public void UpdateRef(string source)
        {
            throw new NotSupportedException();
        }

        public bool DoesBranchContainCommit(string branch, string commit)
        {
            throw new NotImplementedException();
        }

        public Task PostBuild()
        {
            if (_deleteDirectoryAfterBuild)
            {
                var tracer = _traceFactory.GetTracer();
                using (tracer.Step("Repository post-build: Deleting repo folder " + _path))
                {
                    FileSystemHelpers.DeleteDirectorySafe(_path);
                }
            }

            return Task.CompletedTask;
        }
    }
}