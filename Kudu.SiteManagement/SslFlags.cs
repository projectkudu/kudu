using System;

namespace Kudu.SiteManagement
{
    [Flags, System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Flags", Justification = "In this particular case, using just SSL is a bit to abstract to understand what it is.")]
    public enum SslFlags
    {
        None = 0,
        Sni = 1,
        //Note: Not in use as of now.
        CentralCertStore = 1 << 1
    }
}