using Kudu.SiteManagement.Certificates.Wrappers;

namespace Kudu.SiteManagement.Test.Certificates.Fakes
{
    //Note: Simple Mock class instead of having to mock the interface, since IX509Certificate2 essentially is
    //      a "data object" and not a Service, this can sometimes be easier this way.
    public class X509Certificate2Fake : IX509Certificate2
    {
        private byte[] hash;

        public string Thumbprint { get; private set; }
        public string FriendlyName { get; private set; }

        public X509Certificate2Fake(string friendlyName = "DummyFriendlyName", string thumbprint = "DummyThumbprint", byte[] hash = null)
        {
            Thumbprint = thumbprint;
            FriendlyName = friendlyName;
            this.hash = hash ?? new byte[0];
        }

        public byte[] GetCertHash()
        {
            return hash;
        }
    }
}