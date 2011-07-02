using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Kudu.Core;
using Kudu.Core.Git;
using Kudu.Core.Hg;

namespace Kudu.Services.Controllers {
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

        [HttpPost]
        [ActionName("status")]
        public ActionResult GetStatus() {
            return Json(GetRepository().GetStatus(), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("changes")]
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
        public ActionResult Commit(string authorName, string message) {
            return Json(GetRepository().Commit(authorName, message));
        }

        [HttpPost]
        [ActionName("update")]
        public void Update(string id) {
            GetRepository().Update(id);
        }

        private IRepository GetRepository() {
            string path = Path.Combine(HttpRuntime.AppDomainAppPath, @"..\");

            if (String.IsNullOrEmpty(path)) {
                throw new InvalidOperationException("No repository path!");
            }

            if (Directory.EnumerateDirectories(path, ".hg").Any()) {
                return new HgRepository(path);
            }
            return new HybridRepository(path);
        }
    }
}