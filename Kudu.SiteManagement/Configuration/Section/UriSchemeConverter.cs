using System;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;

namespace Kudu.SiteManagement.Configuration.Section
{
    public class UriSchemeConverter : ConfigurationConverterBase
    {
        public override object ConvertFrom(ITypeDescriptorContext ctx, CultureInfo ci, object data)
        {
            UriSchemes scheme;
            if (data == null || !Enum.TryParse(data.ToString(), true, out scheme))
                return UriSchemes.Http;
            return scheme;
        }
    }
}