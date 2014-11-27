namespace Kudu.SiteManagement.Certificates.Wrappers
{
    //Note: Wrapper intention is to facilitate mocking.
    //      This class has mostly been generated with resharper.
    public interface IX509Certificate2
    {
        string FriendlyName { get; }
        string Thumbprint { get; }
        byte[] GetCertHash();
    }
}