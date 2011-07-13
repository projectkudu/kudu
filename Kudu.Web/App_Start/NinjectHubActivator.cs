using System;
using Ninject;
using SignalR.Hubs;

namespace Kudu.Web.App_Start {
    public class NinjectHubActivator : IHubActivator {
        private IKernel _kernel;

        public NinjectHubActivator(IKernel kernel) {
            _kernel = kernel;
        }

        public Hub Create(Type hubType) {
            return (Hub)_kernel.Get(hubType);
        }
    }
}
