using System;
using System.Collections.Generic;
using Ninject;
using SignalR.Infrastructure;

namespace Kudu.Web.App_Start {
    public class NinjectDependencyResolver : IDependencyResolver {
        private IKernel _kernel;

        public NinjectDependencyResolver(IKernel kernel) {
            _kernel = kernel;
        }

        public object GetService(Type serviceType) {
            return _kernel.TryGet(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType) {
            return _kernel.GetAll(serviceType);
        }

        public void Register(Type serviceType, Func<object> activator) {
            _kernel.Bind(serviceType).ToMethod(_ => activator());
        }

        public void Register(Type serviceType, IEnumerable<Func<object>> activators) {
            
        }
    }
}
