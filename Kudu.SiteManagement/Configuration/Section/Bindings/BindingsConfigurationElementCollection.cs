using System;
using System.Collections.Generic;
using System.Configuration;

namespace Kudu.SiteManagement.Configuration.Section.Bindings
{
    public class BindingsConfigurationElementCollection : NamedElementCollection<BindingConfigurationElement>
    {
        protected override object GetElementKey(ConfigurationElement element)
        {
            BindingConfigurationElement binding = element as BindingConfigurationElement;
            if(binding == null)
                throw new ConfigurationErrorsException();

            return binding.Scheme + "://" + binding.Url;
        }

        protected override Type ResolveTypeName(string elementName)
        {
            if (elementName == "applicationBinding")
                return typeof (ApplicationBindingConfigurationElement);

            if (elementName == "serviceBinding")
                return typeof(ServiceBindingConfigurationElement);

            throw new ConfigurationErrorsException();
        }
    }
}