using System;
using System.Threading.Tasks;
using IIS = Microsoft.Web.Administration;

namespace Kudu.SiteManagement
{
    public static class IISExtensions
    {
        private static readonly TimeSpan _waitInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan _maxWaitInterval = TimeSpan.FromMinutes(5);

        public static async Task WaitForState(this IIS.Site site, IIS.ObjectState state)
        {
            await WaitForState(() => site.State, state);
        }

        public static async Task StopAndWait(this IIS.Site site)
        {
            site.Stop();
            await WaitForState(() => site.State, IIS.ObjectState.Stopped);
        }

        public static async Task StartAndWait(this IIS.ApplicationPool appPool)
        {
            appPool.Start();
            await WaitForState(() => appPool.State, IIS.ObjectState.Started);
        }

        public static async Task StopAndWait(this IIS.ApplicationPool appPool)
        {
            appPool.Stop();
            await appPool.WaitForState(IIS.ObjectState.Stopped);
        }

        public static async Task WaitForState(this IIS.ApplicationPool appPool, IIS.ObjectState state)
        {
            await WaitForState(() => appPool.State, state);
        }

        private static async Task WaitForState(Func<IIS.ObjectState> getState, IIS.ObjectState state)
        {
            TimeSpan totalWait = TimeSpan.Zero;

            IIS.ObjectState? currentState;
            while ((currentState = SafeGetState(getState)) != state)
            {
                totalWait += _waitInterval;
                if (totalWait > _maxWaitInterval)
                {
                    throw new InvalidOperationException(String.Format("State unchanged after {0} seconds. Expected state: '{1}', actual state: '{2}'.",
                        totalWait.TotalSeconds, state, currentState));
                }
                await Task.Delay(_waitInterval);
            }
        }

        private static IIS.ObjectState? SafeGetState(Func<IIS.ObjectState> getState)
        {
            try
            {
                return getState();
            }
            catch
            {
                return null;
            }
        }
    }
}
