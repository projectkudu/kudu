using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Mvc.Async
{
  public class TaskAsyncControllerDescriptor : ReflectedControllerDescriptor
  {
    public TaskAsyncControllerDescriptor(Type controllerType)
      : base(controllerType)
    {
    }

    public override ActionDescriptor FindAction(ControllerContext controllerContext, string actionName)
    {
      ActionDescriptor action = base.FindAction(controllerContext, actionName);
      ReflectedActionDescriptor actionDescriptor = action as ReflectedActionDescriptor;
      MethodInfo methodInfo = actionDescriptor != null ? actionDescriptor.MethodInfo : (MethodInfo) null;
      Type c = methodInfo != null ? methodInfo.ReturnType : null;
      if (c != null && typeof (Task).IsAssignableFrom(c))
        return (ActionDescriptor) new TaskAsyncActionDescriptor(methodInfo, actionName, (ControllerDescriptor) this);
      else
        return action;
    }
  }
}
