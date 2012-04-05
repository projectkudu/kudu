using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public class ProgressWriter : IDisposable
    {
        private bool _running;

        public void Start()
        {
            _running = true;

            Delay(TimeSpan.FromSeconds(5)).ContinueWith(task =>
            {
                while (_running)
                {
                    Console.Write(".");

                    Thread.Sleep(1000);
                }
            });
        }

        public void Stop()
        {
            Console.WriteLine();
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
