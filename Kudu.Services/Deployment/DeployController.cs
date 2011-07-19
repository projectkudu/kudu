using System.Web.Mvc;
using Kudu.Core.Deployment;
using Kudu.Services.Infrastructure;

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

        [HttpGet]
        [ActionName("log")]
        public ActionResult GetResults() {
            return Json(_deploymentManager.GetResults(), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("details")]
        public ActionResult GetResult(string id) {
            return Json(_deploymentManager.GetResult(id), JsonRequestBehavior.AllowGet);
        }
    }
}
