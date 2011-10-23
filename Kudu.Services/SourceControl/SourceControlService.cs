using System.Collections.Generic;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.SourceControl;
using Microsoft.ApplicationServer.Http.Dispatcher;

namespace Kudu.Services.SourceControl {
    [ServiceContract]
    public class SourceControlService {
        private readonly IRepository _repository;

        public SourceControlService(IRepository repository) {
            _repository = repository;
        }

        [WebGet(UriTemplate = "id")]
        public string GetCurrentId() {
            return _repository.CurrentId;
        }

        [WebGet(UriTemplate = "branches")]
        public IEnumerable<Branch> GetBranches() {
            return _repository.GetBranches();
        }

        [WebGet(UriTemplate = "status")]
        public IEnumerable<FileStatus> GetStatus() {
            return _repository.GetStatus();
        }

        [WebGet(UriTemplate = "log?index={index}&limit={limit}")]
        public IEnumerable<ChangeSet> GetChanges(int? index, int? limit) {
            IEnumerable<ChangeSet> changeSets = null;
            if (index == null && limit == null) {
                changeSets = _repository.GetChanges();
            }
            else {
                changeSets = _repository.GetChanges(index.Value, limit.Value);
            }

            return changeSets;
        }

        [WebGet(UriTemplate = "details/{*id}")]
        public ChangeSetDetail GetDetails(string id) {
            try {
                return _repository.GetDetails(id);
            }
            catch {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
        }

        [WebGet(UriTemplate = "working")]
        public ChangeSetDetail GetWorkingChanges() {
            return _repository.GetWorkingChanges();
        }

        [WebInvoke(UriTemplate = "add")]
        public void AddFile(SimpleJson.JsonObject input) {
            _repository.AddFile((string)input["path"]);
        }

        [WebInvoke(UriTemplate = "remove")]
        public void RemoveFile(SimpleJson.JsonObject input) {
            _repository.RevertFile((string)input["path"]);
        }

        [WebInvoke]
        public ChangeSet Commit(SimpleJson.JsonObject input) {
            return _repository.Commit((string)input["name"], (string)input["message"]);
        }

        [WebInvoke]
        public void Update(SimpleJson.JsonObject input) {
            _repository.Update((string)input["id"]);
        }

        [WebInvoke]
        public void Push() {
            _repository.Push();
        }
    }
}