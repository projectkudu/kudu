using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Kudu.Core.Infrastructure
{
    internal static class TaskExtensions
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
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
