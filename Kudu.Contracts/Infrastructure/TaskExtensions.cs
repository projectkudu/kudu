using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.Contracts.Infrastructure
{
    public static class TaskExtensions
    {
        public static Task Then(this Task task, Action successor)
        {
            var tcs = new TaskCompletionSource<object>();

            if (task.Status == TaskStatus.RanToCompletion)
            {
                successor();
                tcs.SetResult(null);
                return tcs.Task;
            }
            else if (task.Status == TaskStatus.Faulted)
            {
                tcs.SetException(task.Exception);
                return tcs.Task;
            }

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    tcs.SetException(t.Exception);
                }
                else if (t.IsCanceled)
                {
                    tcs.SetCanceled();
                }
                else
                {
                    try
                    {
                        successor();
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }
            });

            return tcs.Task;
        }

        public static void Catch(this Task task, Action<Exception> handler)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Trace.TraceError(t.Exception.Message);
                    handler(t.Exception);
                }
            }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        public static Task Then<TInnerResult>(this Task<TInnerResult> task, Action<TInnerResult> continuation, CancellationToken cancellationToken = default(CancellationToken))
        {
            return task.ThenImpl(t => ToAsyncVoidTask(() => continuation(t.Result)), cancellationToken);
        }

        public static Task<TOuterResult> Then<TInnerResult, TOuterResult>(this Task<TInnerResult> task, Func<TInnerResult, TOuterResult> continuation, CancellationToken cancellationToken = default(CancellationToken))
        {
            return task.ThenImpl(t => FromResult(continuation(t.Result)), cancellationToken);
        }

        public static Task<TOuterResult> Then<TInnerResult, TOuterResult>(this Task<TInnerResult> task, Func<TInnerResult, Task<TOuterResult>> continuation, CancellationToken cancellationToken = default(CancellationToken))
        {
            return task.ThenImpl(t => continuation(t.Result), cancellationToken);
        }

        private static Task<TOuterResult> ThenImpl<TTask, TOuterResult>(this TTask task, Func<TTask, Task<TOuterResult>> continuation, CancellationToken cancellationToken)
            where TTask : Task
        {
            // Stay on the same thread if we can
            if (task.IsCanceled || cancellationToken.IsCancellationRequested)
            {
                return Canceled<TOuterResult>();
            }
            if (task.IsFaulted)
            {
                return FromErrors<TOuterResult>(task.Exception.InnerExceptions);
            }
            if (task.Status == TaskStatus.RanToCompletion)
            {
                try
                {
                    return continuation(task);
                }
                catch (Exception ex)
                {
                    return FromError<TOuterResult>(ex);
                }
            }

            SynchronizationContext syncContext = SynchronizationContext.Current;

            return task.ContinueWith(innerTask =>
            {
                if (innerTask.IsFaulted)
                {
                    return FromErrors<TOuterResult>(innerTask.Exception.InnerExceptions);
                }
                if (innerTask.IsCanceled)
                {
                    return Canceled<TOuterResult>();
                }

                TaskCompletionSource<Task<TOuterResult>> tcs = new TaskCompletionSource<Task<TOuterResult>>();
                if (syncContext != null)
                {
                    syncContext.Post(state =>
                    {
                        try
                        {
                            tcs.TrySetResult(continuation(task));
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    }, state: null);
                }
                else
                {
                    tcs.TrySetResult(continuation(task));
                }
                return tcs.Task.FastUnwrap();
            }, cancellationToken).FastUnwrap();
        }

        private static Task<TResult> FromResult<TResult>(TResult result)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            tcs.SetResult(result);
            return tcs.Task;
        }

        private static Task<AsyncVoid> ToAsyncVoidTask(Action action)
        {
            return RunSynchronously<AsyncVoid>(() =>
            {
                action();
                return FromResult<AsyncVoid>(default(AsyncVoid));
            });
        }

        private static Task<TResult> FastUnwrap<TResult>(this Task<Task<TResult>> task)
        {
            Task<TResult> innerTask = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
            return innerTask ?? task.Unwrap();
        }

        private static Task<TResult> RunSynchronously<TResult>(Func<Task<TResult>> func, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Canceled<TResult>();
            }

            try
            {
                return func();
            }
            catch (Exception e)
            {
                return FromError<TResult>(e);
            }
        }

        private static Task<TResult> FromError<TResult>(Exception exception)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            tcs.SetException(exception);
            return tcs.Task;
        }

        private static Task<TResult> FromErrors<TResult>(IEnumerable<Exception> exceptions)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            tcs.SetException(exceptions);
            return tcs.Task;
        }

        private static Task<TResult> Canceled<TResult>()
        {
            return CancelCache<TResult>.Canceled;
        }

        private struct AsyncVoid { }

        private static class CancelCache<TResult>
        {
            public static readonly Task<TResult> Canceled = GetCancelledTask();

            private static Task<TResult> GetCancelledTask()
            {
                TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
                tcs.SetCanceled();
                return tcs.Task;
            }
        }
    }
}
