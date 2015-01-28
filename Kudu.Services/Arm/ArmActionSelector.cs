using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.UI;

namespace Kudu.Services.Arm
{
    public class ArmActionSelector : ApiControllerActionSelector
    {
        public override HttpActionDescriptor SelectAction(HttpControllerContext controllerContext)
        {
            // This method selects which action method on the controller to invoke for any specific request.
            // In this case if the request is a PUT and it is an ARM request we will direct it to the intended
            // method name adding an "Arm" to it.
            // For example if the route "/api/continuouswebjobs/myjob" is routed to action/method "CreateContinuousJob"
            // then the ARM version of that method will be "CreateContinuousJobArm" and that is what is invoked when
            // the condition applies.
            if (controllerContext.Request.Method == HttpMethod.Put && ArmUtils.IsArmRequest(controllerContext.Request))
            {
                controllerContext.RouteData.Values["action"] += "Arm";
            }

            return base.SelectAction(controllerContext);
        }
    }
}
