using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Mvc.Async;

namespace Mvc.Async
{
    public class TaskAsyncActionDescriptor : AsyncActionDescriptor
    {
        private static readonly ConcurrentDictionary<Type, Func<object, object>> _taskValueExtractors = new ConcurrentDictionary<Type, Func<object, object>>();
        private readonly string _actionName;
        private readonly ControllerDescriptor _controllerDescriptor;

        public override string ActionName
        {
            get
            {
                return this._actionName;
            }
        }

        public override ControllerDescriptor ControllerDescriptor
        {
            get
            {
                return this._controllerDescriptor;
            }
        }

        public MethodInfo MethodInfo { get; private set; }

        static TaskAsyncActionDescriptor()
        {
        }

        public TaskAsyncActionDescriptor(MethodInfo methodInfo, string actionName, ControllerDescriptor controllerDescriptor)
        {
            this.MethodInfo = methodInfo;
            this._actionName = actionName;
            this._controllerDescriptor = controllerDescriptor;
        }

        public override IAsyncResult BeginExecute(ControllerContext controllerContext, IDictionary<string, object> parameters, AsyncCallback callback, object state)
        {
            Task result = new ReflectedActionDescriptor(this.MethodInfo, this.ActionName, this.ControllerDescriptor).Execute(controllerContext, parameters) as Task;
            if (result == null)
            {
                throw new InvalidOperationException(string.Format("Method {0} should have returned a Task!", (object)this.MethodInfo));
            }
            else
            {
                if (callback != null)
                    result.ContinueWith((Action<Task>)(_ => callback((IAsyncResult)result)));
                return (IAsyncResult)result;
            }
        }

        public override object EndExecute(IAsyncResult asyncResult)
        {
            return TaskAsyncActionDescriptor._taskValueExtractors.GetOrAdd(MethodInfo.ReturnType, new Func<Type, Func<object, object>>(TaskAsyncActionDescriptor.CreateTaskValueExtractor))((object)asyncResult);
        }

        public override object Execute(ControllerContext controllerContext, IDictionary<string, object> parameters)
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return this.MethodInfo.GetCustomAttributes(inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return this.MethodInfo.GetCustomAttributes(attributeType, inherit);
        }

        public override ParameterDescriptor[] GetParameters()
        {
            return (ParameterDescriptor[])Array.ConvertAll<ParameterInfo, ReflectedParameterDescriptor>(this.MethodInfo.GetParameters(), (Converter<ParameterInfo, ReflectedParameterDescriptor>)(pInfo => new ReflectedParameterDescriptor(pInfo, (ActionDescriptor)this)));
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return this.MethodInfo.IsDefined(attributeType, inherit);
        }

        private static Func<object, object> CreateTaskValueExtractor(Type taskType)
        {
            if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                ParameterExpression parameterExpression = Expression.Parameter(typeof(object));
                return Expression.Lambda<Func<object, object>>((Expression)Expression.Convert((Expression)Expression.Property((Expression)Expression.Convert((Expression)parameterExpression, taskType), "Result"), typeof(object)), new ParameterExpression[1]
        {
          parameterExpression
        }).Compile();
            }
            else
                return (Func<object, object>)(theTask =>
                {
                    ((Task)theTask).Wait();
                    return (object)null;
                });
        }
    }
}
