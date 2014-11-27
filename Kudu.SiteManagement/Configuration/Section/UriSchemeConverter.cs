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
            if(data == null)
                return UriScheme.Http;
 
            UriScheme scheme;
            if (Enum.TryParse(data.ToString(), true, out scheme))
                return scheme;

            throw new FormatException("Could not parse '" + data + "', expected http or https.");
        }
    }
}