using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;

namespace Kudu.Services.Infrastructure
{
    internal static class LockExtensions
    {
        public static void LockHttpOperation(this IOperationLock lockObj, Action action, string operationName)
        {
            try
            {
                lockObj.LockOperation(action, operationName, TimeSpan.Zero);
            }
            catch (LockOperationException ex)
            {
                var response = new HttpResponseMessage(HttpStatusCode.Conflict);
                response.Content = new StringContent(ex.Message);
                throw new HttpResponseException(response);
            }
        }

        public static async Task LockHttpOperationAsync(this IOperationLock lockObj, Func<Task> action, string operationName)
        {
            try
            {
                await lockObj.LockOperationAsync(action, operationName, TimeSpan.Zero);
            }
            catch (LockOperationException ex)
            {
                var response = new HttpResponseMessage(HttpStatusCode.Conflict);
                response.Content = new StringContent(ex.Message);
                throw new HttpResponseException(response);
            }
        }
    }
}
