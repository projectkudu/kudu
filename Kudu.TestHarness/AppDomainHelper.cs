using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.TestHarness
{
    public static class AppDomainHelper
    {
        static object _lock;
        static ManualResetEvent _threadCompletion;
        static int _threadCount;

        static AppDomainHelper()
        {
            var appDomain = AppDomain.CurrentDomain;

            // default appdomain unload with process termination
            if (!appDomain.IsDefaultAppDomain())
            {
                _lock = new object();
                _threadCompletion = new ManualResetEvent(true);
                _threadCount = 0;

                appDomain.DomainUnload += delegate
                {
                    // wait for all task completion
                    _threadCompletion.WaitOne(60000);
                };
            }
        }

        public static async Task RunTask(Func<Task> action)
        {
            OnBeginTask();
            await Task.Run(async () =>
            {
                try
                {
                    await action();
                }
                finally
                {
                    OnEndTask();
                }
            });
        }

        static void OnBeginTask()
        {
            if (_lock != null)
            {
                lock (_lock)
                {
                    _threadCompletion.Reset();
                    _threadCount++;
                }
            }
        }

        static void OnEndTask()
        {
            if (_lock != null)
            {
                lock (_lock)
                {
                    _threadCount--;
                    if (_threadCount == 0)
                    {
                        _threadCompletion.Set();
                    }
                }
            }
        }
    }
}