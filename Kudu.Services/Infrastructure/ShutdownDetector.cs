using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Timers;
using System.Web;
using System.Web.Hosting;
using Timer = System.Threading.Timer;

// This code comes from http://katanaproject.codeplex.com/ (src\Microsoft.Owin.Host.SystemWeb\ShutdownDetector.cs)

namespace Kudu.Services.Infrastructure
{
    public class ShutdownDetector : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Timer _checkAppPoolTimer;
        private static readonly TimeSpan _appPoolCheckInterval = TimeSpan.FromSeconds(10);

        public CancellationToken Token
        {
            get { return _cts.Token; }
        }

        public void Initialize()
        {
            try
            {
                // Create a timer for detecting when the app pool has been requested for shutdown.
                // Normally when the appdomain shuts down IRegisteredObject.Stop gets called
                // but ASP.NET waits for requests to end before calling IRegisteredObject.Stop (This can be
                // troublesome for some frameworks like SignalR that keep long running requests alive).
                // This is a more aggresive check to see if the app domain is in the process of being shutdown and
                // we trigger the same cts in that case.
                if (HttpRuntime.UsingIntegratedPipeline &&
                    UnsafeIISMethods.CanDetectAppDomainRestart && _checkAppPoolTimer == null)
                {
                    _checkAppPoolTimer = new Timer(_ =>
                    {
                        if (UnsafeIISMethods.RequestedAppDomainRestart)
                        {
                            // Trigger the cancellation token
                            _cts.Cancel(throwOnFirstException: false);

                            // Stop the timer as we don't need it anymore
                            _checkAppPoolTimer.Dispose();
                        }
                    },
                    state: null,
                    dueTime: _appPoolCheckInterval,
                    period: _appPoolCheckInterval);
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Dispose();

                if (_checkAppPoolTimer != null)
                {
                    _checkAppPoolTimer.Dispose();
                }
            }
        }

        private static class UnsafeIISMethods
        {
            private static Lazy<UnsafeIISMethodsWrapper> _iis = new Lazy<UnsafeIISMethodsWrapper>(() => new UnsafeIISMethodsWrapper());

            public static bool RequestedAppDomainRestart
            {
                get
                {
                    if (!CanDetectAppDomainRestart)
                    {
                        return false;
                    }

                    return !_iis.Value.CheckConfigChanged();
                }
            }

            public static bool CanDetectAppDomainRestart
            {
                get
                {
                    return _iis.Value.CheckConfigChanged != null;
                }
            }

            private class UnsafeIISMethodsWrapper
            {
                public Func<bool> CheckConfigChanged { get; private set; }

                public UnsafeIISMethodsWrapper()
                {
                    // Private reflection to get the UnsafeIISMethods
                    var type = Type.GetType("System.Web.Hosting.UnsafeIISMethods, System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

                    if (type == null)
                    {
                        return;
                    }

                    // This method can tell us if ASP.NET requested and app domain shutdown
                    MethodInfo methodInfo = type.GetMethod("MgdHasConfigChanged", BindingFlags.NonPublic | BindingFlags.Static);

                    if (methodInfo == null)
                    {
                        // Method signuature changed so just bail
                        return;
                    }

                    try
                    {
                        CheckConfigChanged = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), methodInfo);
                    }
                    catch
                    {
                        // We failed to create the delegate so we can't do the check 
                        // reliably
                    }
                }
            }
        }
    }
}
