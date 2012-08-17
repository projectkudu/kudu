using System.Web.Mvc;
using System.Web.Mvc.Async;
using System.Threading.Tasks;

namespace Mvc.Async
{
    [NoAsyncTimeout]
    public class TaskAsyncController : AsyncController
    {
        protected override IActionInvoker CreateActionInvoker()
        {
            return (IActionInvoker)new TaskAsyncController.TaskAsyncControllerActionInvoker();
        }

        private sealed class TaskAsyncControllerActionInvoker : AsyncControllerActionInvoker
        {
            protected override ControllerDescriptor GetControllerDescriptor(ControllerContext controllerContext)
            {
                return (ControllerDescriptor)new TaskAsyncControllerDescriptor(controllerContext.Controller.GetType());
            }
        }

        protected Task<ActionResult> HttpNotFoundAsync()
        {
            var tcs = new TaskCompletionSource<ActionResult>();
            tcs.SetResult(HttpNotFound());
            return tcs.Task;
        }

        protected Task<ActionResult> RedirectToActionAsync(string actionName, object routeValues)
        {
            var tcs = new TaskCompletionSource<ActionResult>();
            tcs.SetResult(RedirectToAction(actionName, routeValues));
            return tcs.Task;
        }
    }
}
