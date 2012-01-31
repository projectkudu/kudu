using System.Collections.Generic;
using System.ComponentModel;
using System.Json;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.SourceControl;
using Microsoft.ApplicationServer.Http.Dispatcher;

namespace Kudu.Services.SourceControl
{
    [ServiceContract]
    public class SourceControlService
    {
        private readonly IRepository _repository;

        public SourceControlService(IRepository repository)
        {
            _repository = repository;
        }

        [Description("Gets the current repository id")]
        [WebGet(UriTemplate = "id")]
        public string GetCurrentId()
        {
            return _repository.CurrentId;
        }

        [Description("Gets the repository branches.")]
        [WebGet(UriTemplate = "branches")]
        public IEnumerable<Branch> GetBranches()
        {
            return _repository.GetBranches();
        }

        [Description("Gets the repository status")]
        [WebGet(UriTemplate = "status")]
        public IEnumerable<FileStatus> GetStatus()
        {
            return _repository.GetStatus();
        }

        [Description("Gets the changes on this repository within the specified range (optional).")]
        [WebGet(UriTemplate = "log?index={index}&limit={limit}")]
        public IEnumerable<ChangeSet> GetChanges(int? index, int? limit)
        {
            IEnumerable<ChangeSet> changeSets = null;
            if (index == null && limit == null)
            {
                changeSets = _repository.GetChanges();
            }
            else
            {
                changeSets = _repository.GetChanges(index.Value, limit.Value);
            }

            return changeSets;
        }

        [Description("Gets the details of a specific change based on its id.")]
        [WebGet(UriTemplate = "details/{*id}")]
        public ChangeSetDetail GetDetails(string id)
        {
            try
            {
                return _repository.GetDetails(id);
            }
            catch
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
        }

        [Description("Gets the current pending changes.")]
        [WebGet(UriTemplate = "working")]
        public ChangeSetDetail GetWorkingChanges()
        {
            return _repository.GetWorkingChanges();
        }

        [Description("Adds a file to the pending changes.")]
        [WebInvoke(UriTemplate = "add")]
        public void AddFile(JsonObject input)
        {
            _repository.AddFile((string)input["path"]);
        }

        [Description("Reverts a file pending changes.")]
        [WebInvoke(UriTemplate = "remove")]
        public void RemoveFile(JsonObject input)
        {
            _repository.RevertFile((string)input["path"]);
        }

        [Description("Commits pending changes.")]
        [WebInvoke(UriTemplate = "commit")]
        public ChangeSet Commit(JsonObject input)
        {
            return _repository.Commit((string)input["name"], (string)input["message"]);
        }

        [Description("Updates the repository to the specified changes.")]
        [WebInvoke(UriTemplate = "update")]
        public void Update(JsonObject input)
        {
            _repository.Update((string)input["id"]);
        }

        [Description("Updates the repository to the default changeset.")]
        [WebInvoke(UriTemplate = "update")]
        public void Update()
        {
            _repository.Update();
        }

        [Description("Pushes all commited changes to the remote repository.")]
        [WebInvoke(UriTemplate = "push")]
        public void Push()
        {
            _repository.Push();
        }
    }
}