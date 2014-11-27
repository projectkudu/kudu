using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Kudu.SiteManagement.Certificates.Wrappers
{
    //Note: Wrapper intention is to facilitate mocking.
    //      This class has mostly been generated with resharper.
    public sealed class X509StoreWrapper : IX509Store
    {
        private readonly X509Store _store;

        public string Name
        {
            get { return _store.Name; }
        }

        public X509StoreWrapper(X509Store store)
        {
            _store = store;
        }

        public void Open(OpenFlags flags)
        {
            _store.Open(flags);
        }

        public void Close()
        {
            _store.Close();
        }

        public IX509Certificate2Collection Certificates
        {
            get { return new X509Certificate2CollectionWrapper(_store.Certificates); }
        }
    }
}