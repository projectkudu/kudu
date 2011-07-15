using System.Web.Mvc;
using Kudu.Core.Deployment;

namespace Kudu.Services.Deployment {
    [FormattedExceptionFilter]
    public class DeployController : Controller {
        private readonly IDeploymentManager _deploymentManager;
        private readonly IDeployer _deployer;

        public DeployController(IDeploymentManager deploymentManager, 
                                IDeployerFactory factory) {
            _deploymentManager = deploymentManager;
            _deployer = factory.CreateDeployer();
        }

        [HttpPost]
        public void Index(string id) {
            _deployer.Deploy(id);
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
