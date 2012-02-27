using System.Web.Mvc;
using System.Web.Mvc.Async;

namespace Mvc.Async
{
  [NoAsyncTimeout]
  public class TaskAsyncController : AsyncController
  {
    protected override IActionInvoker CreateActionInvoker()
    {
      return (IActionInvoker) new TaskAsyncController.TaskAsyncControllerActionInvoker();
    }

    private sealed class TaskAsyncControllerActionInvoker : AsyncControllerActionInvoker
    {
      protected override ControllerDescriptor GetControllerDescriptor(ControllerContext controllerContext)
      {
        return (ControllerDescriptor) new TaskAsyncControllerDescriptor(controllerContext.Controller.GetType());
      }
    }
  }
}
