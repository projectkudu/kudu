﻿// <copyright file="ShutdownDetector.cs" company="Microsoft Open Technologies, Inc.">
// Copyright 2011-2013 Microsoft Open Technologies, Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Web;
using System.Web.Hosting;

// This code comes from https://katanaproject.codeplex.com/SourceControl/latest#src/Microsoft.Owin.Host.SystemWeb/ShutdownDetector.cs

 namespace Kudu.Services.Infrastructure
{
    public class ShutdownDetector : IRegisteredObject, IDisposable
    {
        private const string TraceName = "Microsoft.Owin.Host.SystemWeb.ShutdownDetector";

        private readonly CancellationTokenSource _cts;
        private IDisposable _checkAppPoolTimer;

        public ShutdownDetector()
        {
            _cts = new CancellationTokenSource();
        }

        internal CancellationToken Token
        {
            get { return _cts.Token; }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Initialize must not throw")]
        public void Initialize()
        {
            try
            {
                HostingEnvironment.RegisterObject(this);

                // Normally when the AppDomain shuts down IRegisteredObject.Stop gets called, except that
                // ASP.NET waits for requests to end before calling IRegisteredObject.Stop. This can be
                // troublesome for some frameworks like SignalR that keep long running requests alive.
                // These are more aggressive checks to see if the app domain is in the process of being shutdown and
                // we trigger the same cts in that case.
                if (HttpRuntime.UsingIntegratedPipeline)
                {
                    if (RegisterForStopListeningEvent())
                    {
                    }
                    else if (UnsafeIISMethods.CanDetectAppDomainRestart)
                    {
                        // Create a timer for polling when the app pool has been requested for shutdown.
#if NET40
                        // Use the existing timer
                        _checkAppPoolTimer = SharedTimer.StaticTimer.Register(CheckForAppDomainRestart, state: null);
#else
                        _checkAppPoolTimer = new Timer(CheckForAppDomainRestart, state: null,
                            dueTime: TimeSpan.FromSeconds(10), period: TimeSpan.FromSeconds(10));
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        // Note: When we have a compilation that targets .NET 4.5.1, implement IStopListeningRegisteredObject
        // instead of reflecting for HostingEnvironment.StopListening.
        private bool RegisterForStopListeningEvent()
        {
            EventInfo stopEvent = typeof(HostingEnvironment).GetEvent("StopListening");
            if (stopEvent == null)
            {
                return false;
            }
            stopEvent.AddEventHandler(null, new EventHandler(StopListening));
            return true;
        }

        private void StopListening(object sender, EventArgs e)
        {
            Cancel();
        }

        private void CheckForAppDomainRestart(object state)
        {
            if (UnsafeIISMethods.RequestedAppDomainRestart)
            {
                Cancel();
            }
        }

        public void Stop(bool immediate)
        {
            Cancel();
            HostingEnvironment.UnregisterObject(this);
        }

        private void Cancel()
        {
            // Stop the timer as we don't need it anymore
            if (_checkAppPoolTimer != null)
            {
                _checkAppPoolTimer.Dispose();
            }

            // Trigger the cancellation token
            try
            {
                _cts.Cancel(throwOnFirstException: false);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (AggregateException ag)
            {
                Debug.WriteLine(ag.Message);
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
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

        internal static class UnsafeIISMethods
        {
            private static readonly Lazy<UnsafeIISMethodsWrapper> IIS = new Lazy<UnsafeIISMethodsWrapper>(() => new UnsafeIISMethodsWrapper());

            public static bool RequestedAppDomainRestart
            {
                get
                {
                    if (IIS.Value.CheckConfigChanged == null)
                    {
                        return false;
                    }

                    return !IIS.Value.CheckConfigChanged();
                }
            }

            public static bool CanDetectAppDomainRestart
            {
                get { return IIS.Value.CheckConfigChanged != null; }
            }

            private class UnsafeIISMethodsWrapper
            {
                public UnsafeIISMethodsWrapper()
                {
                    // Private reflection to get the UnsafeIISMethods
                    Type type = Type.GetType("System.Web.Hosting.UnsafeIISMethods, System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

                    if (type == null)
                    {
                        return;
                    }

                    // This method can tell us if ASP.NET requested and app domain shutdown
                    MethodInfo methodInfo = type.GetMethod("MgdHasConfigChanged", BindingFlags.NonPublic | BindingFlags.Static);

                    if (methodInfo == null)
                    {
                        // Method signature changed so just bail
                        return;
                    }

                    try
                    {
                        CheckConfigChanged = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), methodInfo);
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (MissingMethodException)
                    {
                    }
                    catch (MethodAccessException)
                    {
                    }
                    // If we failed to create the delegate we can't do the check reliably
                }

                public Func<bool> CheckConfigChanged { get; private set; }
            }
        }
    }
}
