using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SourceControl.Hg;

namespace Kudu.Services.Controllers {
    [JsonExceptionFilter]
    public class ScmController : Controller {
        [HttpGet]
        [ActionName("id")]
        public string GetCurrentId() {
            return GetRepository().CurrentId;
        }

        [HttpPost]
        [ActionName("init")]
        public void Initialize() {
            GetRepository().Initialize();
        }

        [HttpGet]
        [ActionName("branches")]
        public ActionResult GetBranches() {
            return Json(GetRepository().GetBranches(), JsonRequestBehavior.AllowGet);
        }

        [ActionName("status")]
        public ActionResult GetStatus() {
            return Json(GetRepository().GetStatus(), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("log")]
        public ActionResult GetChanges(int? index, int? limit) {
            IEnumerable<ChangeSet> changeSets = null;
            if (index == null && limit == null) {
                changeSets = GetRepository().GetChanges();
            }
            else {
                changeSets = GetRepository().GetChanges(index.Value, limit.Value);
            }

            return Json(changeSets, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("details")]
        public ActionResult GetDetails(string id) {
            try {
                return Json(GetRepository().GetDetails(id), JsonRequestBehavior.AllowGet);
            }
            catch {
                return HttpNotFound();
            }
        }

        [HttpGet]
        [ActionName("working")]
        public ActionResult GetWorkingChanges() {
            return Json(GetRepository().GetWorkingChanges(), JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ActionName("add")]
        public void AddFile(string path) {
            GetRepository().AddFile(path);
        }

        [HttpPost]
        [ActionName("remove")]
        public void RemoveFile(string path) {
            GetRepository().RemoveFile(path);
        }

        [HttpPost]
        [ActionName("commit")]
        [ValidateInput(false)]
        public ActionResult Commit(string name, string message) {
            return Json(GetRepository().Commit(name, message));
        }

        [HttpPost]
        [ActionName("update")]
        public void Update(string id) {
            GetRepository().Update(id);
        }

        private IRepository GetRepository() {
            string path = Path.Combine(GetRootPath(), @"repository");

            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            if (Directory.EnumerateDirectories(path, ".hg").Any()) {
                return new HgRepository(path);
            }
            return new HybridRepository(path);
        }

        private string GetRootPath() {
            // Temporary path (under bin folder so we don't need to ignore it in source control)
            string path = Path.Combine(HttpRuntime.BinDirectory, "_root");

            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }
}