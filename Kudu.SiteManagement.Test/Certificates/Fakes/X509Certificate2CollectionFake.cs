using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Kudu.SiteManagement.Certificates.Wrappers;

namespace Kudu.SiteManagement.Test.Certificates.Fakes
{
    //Note: Simple Mock class instead of having to mock the interface, since IX509Certificate2 essentially is
    //      a "data collection" and not a Service, this can sometimes be easier this way.
    public class X509Certificate2CollectionFake : List<IX509Certificate2>, IX509Certificate2Collection
    {
        public X509Certificate2CollectionFake()
        {
        }

        public X509Certificate2CollectionFake(IEnumerable<IX509Certificate2> collection)
            : base(collection)
        {
        }

        public IX509Certificate2Collection Find(X509FindType findType, object findValue, bool validOnly)
        {
            switch (findType)
            {
                case X509FindType.FindByThumbprint:
                    return new X509Certificate2CollectionFake(this.Where(c => c.Thumbprint == (string)findValue));
                case X509FindType.FindBySubjectName:
                case X509FindType.FindBySubjectDistinguishedName:
                case X509FindType.FindByIssuerName:
                case X509FindType.FindByIssuerDistinguishedName:
                case X509FindType.FindBySerialNumber:
                case X509FindType.FindByTimeValid:
                case X509FindType.FindByTimeNotYetValid:
                case X509FindType.FindByTimeExpired:
                case X509FindType.FindByTemplateName:
                case X509FindType.FindByApplicationPolicy:
                case X509FindType.FindByCertificatePolicy:
                case X509FindType.FindByExtension:
                case X509FindType.FindByKeyUsage:
                case X509FindType.FindBySubjectKeyIdentifier:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException("findType");
            }
        }
    }
}