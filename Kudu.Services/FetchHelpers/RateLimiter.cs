using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.Services.FetchHelpers
{
    /// <summary>
    /// Allow limit amount of action during the given internal
    /// </summary>
    public sealed class RateLimiter : IDisposable
    {
        private readonly int _limit;
        private readonly TimeSpan _interval;
        private readonly SemaphoreSlim _semaphore;
        private int _current = 0;
        private DateTime _last = DateTime.UtcNow;

        public RateLimiter(int limit, TimeSpan interval)
        {
            _limit = limit;
            _interval = interval;
            _semaphore = new SemaphoreSlim(1, 1);
        }

        public async Task ThrottleAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                await ThrottleCore();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task ThrottleCore()
        {
            _current++;
            if (_current > _limit)
            {
                DateTime now = DateTime.UtcNow;
                TimeSpan ts = now - _last;
                TimeSpan waitDuration = _interval - ts;

                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                    _last = DateTime.UtcNow;
                }
                else
                {
                    _last = now;
                }

                _current = 0;
            }
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
