using System;
using System.Collections.Generic;
using System.Reflection;

namespace Kudu.Client.Infrastructure
{
    public class HttpResponseResult<T>
    {
        public IDictionary<string, IEnumerable<string>> Headers { get; set; }
        public T Body { get; set; }

        private HttpResponseResult(IDictionary<string, IEnumerable<string>> headers, T body)
        {
            Headers = headers;
            Body = body;
        }
    }

    /// <summary>
    /// <para>CA1000: Do not declare static members on generic types</para>
    /// <para>https://msdn.microsoft.com/en-us/library/ms182139.aspx</para>
    /// </summary>
    internal static class HttpResponseResultUtils
    {
        public static bool IsTypeOfHttpResponseRresult(Type t)
        {
            var expectedType = typeof(HttpResponseResult<>);
            return t.IsGenericType
                && t.GenericTypeArguments.Length == 1
                && t.GetGenericTypeDefinition() == expectedType;
        }

        public static object CreateHttpResponseResultInstance(Type httpResponseResultType, IDictionary<string, IEnumerable<string>> headers, object body)
        {
            if (!IsTypeOfHttpResponseRresult(httpResponseResultType))
            {
                throw new ArgumentException(string.Format("Unexpected HttpResponseResult type: '{0}'", httpResponseResultType.FullName));
            }

            ConstructorInfo ctorInfo = httpResponseResultType.GetConstructor(
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: new Type[] { typeof(IDictionary<string, IEnumerable<string>>), httpResponseResultType.GenericTypeArguments[0] },
                modifiers: null);

            return ctorInfo.Invoke(new object[] { headers, body });
        }
    }
}
