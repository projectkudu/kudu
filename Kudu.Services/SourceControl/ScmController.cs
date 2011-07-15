using System.Collections.Generic;
using System.Web.Mvc;
using Kudu.Core.SourceControl;

namespace Kudu.Services.SourceControl {
    [FormattedExceptionFilter]
    public class ScmController : Controller {
        private readonly IRepository _repository;
        private readonly IRepositoryManager _repositoryManager;

        public ScmController(IRepositoryManager repositoryManager) {
            _repositoryManager = repositoryManager;
            _repository = repositoryManager.GetRepository() ?? NullRepository.Instance;
        }

        [HttpPost]
        public void Create(RepositoryType type) {
            _repositoryManager.CreateRepository(type);
        }

        [HttpPost]
        public void Delete() {
            _repositoryManager.Delete();
        }

        [HttpGet]
        [ActionName("kind")]
        public ActionResult GetRepositoryType() {
            return Json(_repositoryManager.GetRepositoryType(), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("id")]
        public string GetCurrentId() {
            return _repository.CurrentId;
        }

        [HttpGet]
        [ActionName("branches")]
        public ActionResult GetBranches() {
            return Json(_repository.GetBranches(), JsonRequestBehavior.AllowGet);
        }

        [ActionName("status")]
        public ActionResult GetStatus() {
            return Json(_repository.GetStatus(), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("log")]
        public ActionResult GetChanges(int? index, int? limit) {
            IEnumerable<ChangeSet> changeSets = null;
            if (index == null && limit == null) {
                changeSets = _repository.GetChanges();
            }
            else {
                changeSets = _repository.GetChanges(index.Value, limit.Value);
            }

            return Json(changeSets, JsonRequestBehavior.AllowGet);
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
        public ActionResult GetWorkingChanges() {
            return Json(_repository.GetWorkingChanges(), JsonRequestBehavior.AllowGet);
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
        [ActionName("commit")]
        [ValidateInput(false)]
        public ActionResult Commit(string name, string message) {
            return Json(_repository.Commit(name, message));
        }

        [HttpPost]
        [ActionName("update")]
        public void Update(string id) {
            _repository.Update(id);
        }
    }
}