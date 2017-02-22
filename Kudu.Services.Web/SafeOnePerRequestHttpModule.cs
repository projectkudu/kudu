namespace Kudu.Services.Web
{
    using System.Web;
    using Ninject.Activation.Caching;
    using Ninject;

    /// <summary>
    /// Based on Ninject.Web.Common's OnePerRequestHttpModule
    /// (https://github.com/ninject/Ninject.Web.Common/blob/14fc90d197f02a528ab81eb2a3f12dccdfacb642/src/Ninject.Web.Common/OnePerRequestHttpModule.cs),
    /// but does a null check on HttpContext.Current before calling MapKernels. On Linux/Mono, HttpContext.Current is occasionally null
    /// in the EndRequest stage of the pipeline due to a bug, and the call to MapKernels throws.
    /// If HttpContext.Current is null, disposal of InRequestScope resources will still happen, it just won't be as aggressive.
    /// </summary>
    public sealed class SafeOnePerRequestHttpModule : GlobalKernelRegistration, IHttpModule
    {
        public void Init(HttpApplication application)
        {
            application.EndRequest += (o, e) => this.DeactivateInstancesForCurrentHttpRequest();
        }

        public void DeactivateInstancesForCurrentHttpRequest()
        {
            var context = HttpContext.Current;
            if (context != null)
            {
                this.MapKernels(kernel => kernel.Components.Get<ICache>().Clear(context));
            }
        }

        public void Dispose()
        {
        }
    }
}
