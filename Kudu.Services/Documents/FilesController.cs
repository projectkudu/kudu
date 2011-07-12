using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Kudu.Core.Editor;

namespace Kudu.Services.Documents {
    [JsonExceptionFilter]
    public class FilesController : Controller {
        private readonly PhysicalFileSystem _fileSystem;

        public FilesController(ILocationProvider locationProvider) {
            _fileSystem = new PhysicalFileSystem(locationProvider.RepositoryRoot);
        }

        [HttpGet]
        [ActionName("index")]
        public ActionResult GetFiles(string path) {
            if (String.IsNullOrEmpty(path)) {
                return Json(_fileSystem.GetFiles(), JsonRequestBehavior.AllowGet);
            }
            return Content(_fileSystem.ReadAllText(path));
        }

        [HttpPost]
        public void Save(string path, string content) {
            _fileSystem.WriteAllText(path, content);
        }

        [HttpPost]
        public void Delete(string path) {
            _fileSystem.Delete(path);
        }
    }
}
