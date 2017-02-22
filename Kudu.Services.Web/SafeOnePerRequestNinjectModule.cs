namespace Kudu.Services.Web
{
    using Ninject;

    /// <summary>
    /// Globally registers SafeOnePerRequestHttpModule in the same way that Ninject.Web.Common's
    /// WebCommonNinjectModule registers its own implementation.
    /// Note that the Ninject implementation is still registered (automatically via assembly
    /// reflection on Ninject.Web.*), but it won't do anything at runtime since we don't
    /// register its HttpModule with the application.
    /// </summary>
    public class SafeOnePerRequestNinjectModule : GlobalKernelRegistrationModule<SafeOnePerRequestHttpModule>
    {
    }
}