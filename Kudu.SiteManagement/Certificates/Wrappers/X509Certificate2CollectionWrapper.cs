using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Kudu.SiteManagement.Certificates.Wrappers
{
    //Note: Wrapper intention is to facilitate mocking.
    //      This class has mostly been generated with resharper.
    public sealed class X509Certificate2CollectionWrapper : IX509Certificate2Collection
    {
        private readonly X509Certificate2Collection _collection;

        public X509Certificate2CollectionWrapper(X509Certificate2Collection collection)
        {
            _collection = collection;
        }

        public IX509Certificate2Collection Find(X509FindType findType, object findValue, bool validOnly)
        {
            return new X509Certificate2CollectionWrapper(_collection.Find(findType, findValue, validOnly));
        }

        public IEnumerator<IX509Certificate2> GetEnumerator()
        {


            return _collection
                .OfType<X509Certificate2>()
                .Select(cert => new X509Certificate2Wrapper(cert))
                .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}