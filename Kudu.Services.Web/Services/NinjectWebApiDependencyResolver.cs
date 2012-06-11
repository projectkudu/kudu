using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Web.Http.Dependencies;
using Ninject;
using Ninject.Syntax;

namespace Kudu.Services.Web.Services
{
    public class NinjectDependencyScope : IDependencyScope
    {
        private IResolutionRoot _resolver;

        internal NinjectDependencyScope(IResolutionRoot resolver)
        {
            _resolver = resolver;
        }

        public void Dispose()
        {
            IDisposable disposable = _resolver as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }

            _resolver = null;
        }

        public object GetService(Type serviceType)
        {
            if (_resolver == null)
            {
                throw new ObjectDisposedException("this", "This scope has already been disposed");
            }

            return _resolver.TryGet(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            if (_resolver == null)
            {
                throw new ObjectDisposedException("this", "This scope has already been disposed");
            }

            return _resolver.GetAll(serviceType);
        }
    }

    public class NinjectWebApiDependencyResolver : NinjectDependencyScope, IDependencyResolver
    {
        private readonly IKernel _kernel;

        public NinjectWebApiDependencyResolver(IKernel kernel)
            : base(kernel)
        {
            _kernel = kernel;
        }

        public IDependencyScope BeginScope()
        {
            return new NinjectDependencyScope(_kernel.BeginBlock());
        }
    }
}