using System.Collections.Generic;
using System.Web.Mvc;
using Kudu.Core.SourceControl;
using Kudu.Services.Infrastructure;
using Kudu.Core.SourceControl.Hg;

namespace Kudu.Services.SourceControl {
    [FormattedExceptionFilter]
    public class ScmController : KuduController {
        private readonly IRepository _repository;
        private readonly IRepositoryManager _repositoryManager;
        private readonly IHgServer _server;

        public ScmController(IRepositoryManager repositoryManager,
                             IHgServer server) {
            _repositoryManager = repositoryManager;
            _repository = repositoryManager.GetRepository() ?? NullRepository.Instance;
            _server = server;
        }

        [HttpPost]
        public void Create(RepositoryType type) {
            _repositoryManager.CreateRepository(type);
        }

        [HttpPost]
        public void Delete() {
            // Stop the server (will no-op if nothing is running)
            _server.Stop();
            _repositoryManager.Delete();
        }

        [HttpGet]
        [ActionName("kind")]
        public RepositoryType GetRepositoryType() {
            return _repositoryManager.GetRepositoryType();
        }

        [HttpGet]
        [ActionName("id")]
        public string GetCurrentId() {
            return _repository.CurrentId;
        }

        [HttpGet]
        [ActionName("branches")]
        public IEnumerable<Branch> GetBranches() {
            return _repository.GetBranches();
        }

        [ActionName("status")]
        public IEnumerable<FileStatus> GetStatus() {
            return _repository.GetStatus();
        }

        [HttpGet]
        [ActionName("log")]
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

        [HttpGet]
        [ActionName("details")]
        public ActionResult GetDetails(string id) {
            try {
                return Json(_repository.GetDetails(id), JsonRequestBehavior.AllowGet);
            }
            catch {
                return HttpNotFound();
            }
        }

        [HttpGet]
        [ActionName("working")]
        public ChangeSetDetail GetWorkingChanges() {
            return _repository.GetWorkingChanges();
        }

        [HttpPost]
        [ActionName("add")]
        public void AddFile(string path) {
            _repository.AddFile(path);
        }

        [HttpPost]
        [ActionName("remove")]
        public void RemoveFile(string path) {
            _repository.RevertFile(path);
        }

        [HttpPost]
        [ValidateInput(false)]
        public ChangeSet Commit(string name, string message) {
            return _repository.Commit(name, message);
        }

        [HttpPost]
        public void Update(string id) {
            _repository.Update(id);
        }
    }
}