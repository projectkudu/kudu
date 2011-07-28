using System.Web.Mvc;
using Kudu.Core.Deployment;
using Kudu.Services.Infrastructure;
using System;

namespace Kudu.Services.Deployment {
    [FormattedExceptionFilter]
    public class DeployController : KuduController {
        private readonly IDeploymentManager _deploymentManager;

        public DeployController(IDeploymentManager deploymentManager) {
            _deploymentManager = deploymentManager;
        }

        [HttpPost]
        [ActionName("index")]
        public void Deploy(string id) {
            _deploymentManager.Deploy(id);
        }

        [HttpPost]
        [ActionName("new")]
        public void CreateBuild(string id) {
            _deploymentManager.Build(id);
        }

        [HttpGet]
        [ActionName("log")]
        public ActionResult GetResults(string id) {
            if (String.IsNullOrEmpty(id)) {
                return Json(_deploymentManager.GetResults(), JsonRequestBehavior.AllowGet);
            }
            return Content(_deploymentManager.GetLog(id));
        }

        [HttpGet]
        [ActionName("details")]
        public ActionResult GetResult(string id) {
            return Json(_deploymentManager.GetResult(id), JsonRequestBehavior.AllowGet);
        }
    }
}
