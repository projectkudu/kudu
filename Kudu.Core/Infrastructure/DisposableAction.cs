using System;
using System.Diagnostics.CodeAnalysis;

namespace Kudu.Core.Infrastructure
{
    internal class DisposableAction : IDisposable
    {
        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static readonly IDisposable Noop = new DisposableAction(() => { });

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
