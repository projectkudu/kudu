using IIS = Microsoft.Web.Administration;

namespace Kudu.SiteManagement
{
    public static class IISExtensions
    {
        public static void StartAndWait(this IIS.Site site)
        {
            var wait = new PollingWait(() => site.Start(), () => site.State == IIS.ObjectState.Started);

            wait.Invoke();
        }

        public static void StopAndWait(this IIS.Site site)
        {
            var wait = new PollingWait(() => site.Stop(), () => site.State == IIS.ObjectState.Stopped);

            wait.Invoke();
        }

        public static void StartAndWait(this IIS.ApplicationPool appPool)
        {
            var wait = new PollingWait(() => appPool.Start(), () => appPool.State == IIS.ObjectState.Started);

            wait.Invoke();
        }

        public static void StopAndWait(this IIS.ApplicationPool appPool)
        {
            var wait = new PollingWait(() => appPool.Stop(), () => appPool.State == IIS.ObjectState.Stopped);

            wait.Invoke();
        }

        public static void WaitForState(this IIS.ApplicationPool appPool, IIS.ObjectState state)
        {
            new PollingWait(() => { }, () => appPool.State == state).Invoke();
        }

        public static void WaitForState(this IIS.Site site, IIS.ObjectState state)
        {
            new PollingWait(() => { }, () => site.State == state).Invoke();
        }
    }
}
