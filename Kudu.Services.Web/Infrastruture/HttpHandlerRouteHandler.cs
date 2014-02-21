using System;
using System.Web;
using System.Web.Routing;
using Ninject;

namespace Kudu.Services.Web.Infrastructure
{
    public class HttpHandlerRouteHandler : IRouteHandler
    {
        private readonly Type _handlerType;
        private readonly IKernel _kernel;

        public HttpHandlerRouteHandler(IKernel kernel, Type handlerType)
        {
            _kernel = kernel;
            _handlerType = handlerType;
        }

        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            return (IHttpHandler)_kernel.Get(_handlerType);
        }
    }
}