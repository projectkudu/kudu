using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public class ProgressWriter : IDisposable
    {
        private bool _running;
        private bool _printed;

        public void Start()
        {
            _running = true;

            Delay(TimeSpan.FromSeconds(5)).ContinueWith(task =>
            {
                while (_running)
                {
                    Console.Write(".");

                    _printed = true;
                    Thread.Sleep(1000);
                }
            });
        }

        public void Stop()
        {
            if (_printed)
            {
                // Only print the new line if we printed any progress at all
                Console.WriteLine();
            }
            _running = false;
        }

        public void Dispose()
        {
            Stop();
        }

        private static Task Delay(TimeSpan timeOut)
        {
            var tcs = new TaskCompletionSource<object>();

            var timer = new Timer(tcs.SetResult,
            null,
            timeOut,
            TimeSpan.FromMilliseconds(-1));

            return tcs.Task.ContinueWith(_ =>
            {
                timer.Dispose();
            },
            TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
