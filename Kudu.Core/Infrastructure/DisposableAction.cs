using System;

namespace Kudu.Core.Infrastructure
{
    internal class DisposableAction : IDisposable
    {
        internal static IDisposable Noop = new DisposableAction(() => { });

        private readonly Action _action;
        public DisposableAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            _action();
        }
    }
}
