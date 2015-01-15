using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Kudu.Contracts.Tracing;
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
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;
        private readonly HttpContextBase _httpContext;
        private readonly string _changeSetKey;

        public NullRepository(IEnvironment environment, ITraceFactory traceFactory, HttpContextBase httpContext)
        {
            _environment = environment;
            _traceFactory = traceFactory;
            _httpContext = httpContext;
            _changeSetKey = Path.Combine(_environment.RepositoryPath, "changeset");
        }

        public string CurrentId
        {
            get
            {
                var changeSet = (ChangeSet)_httpContext.Items[_changeSetKey];
                return changeSet == null ? null : changeSet.Id;
            }
        }

        public string RepositoryPath
        {
            get { return _environment.RepositoryPath; }
        }

        public RepositoryType RepositoryType
        {
            get { return RepositoryType.None; }
        }

        public bool Exists
        {
            get { return true; }
        }

        public void Initialize()
        {
            // no-op
        }

        public ChangeSet GetChangeSet(string id)
        {
            var changeSet = (ChangeSet)_httpContext.Items[_changeSetKey];
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

                // The lifetime is per request
                _httpContext.Items[_changeSetKey] = changeSet;

                tracer.Trace("Commit id: " + changeSet.Id, new Dictionary<string, string>
                {
                    { "Messsage", changeSet.Message },
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
            _httpContext.Items.Remove(_changeSetKey);
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
    }
}