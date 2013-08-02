using System;
using System.Threading.Tasks;
using Xunit;

namespace Kudu
{
    public class ExceptionAssert
    {
        public static async Task<TException> ThrowsAsync<TException>(Func<Task> func) where TException : Exception
        {
            Exception actualException = null;
            try
            {
                await func();
            }
            catch (Exception ex)
            {
                actualException = ex;
            }

            return Assert.Throws<TException>(
                () =>
                {
                    Assert.NotNull(actualException);
                    throw actualException;
                });
        }
    }
}
