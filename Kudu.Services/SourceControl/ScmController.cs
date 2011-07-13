using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SourceControl.Hg;

namespace Kudu.Services.SourceControl {
    [JsonExceptionFilter]
    public class ScmController : Controller {
        private readonly IRepository _repository;
        public ScmController(IRepository repository) {
            _repository = repository;
        }

        [HttpGet]
        [ActionName("id")]
        public string GetCurrentId() {
            return _repository.CurrentId;
        }

        [HttpPost]
        [ActionName("init")]
        public void Initialize() {
            _repository.Initialize();
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
            _repository.RemoveFile(path);
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