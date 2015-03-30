using Kudu.SiteManagement.Configuration.Section.Bindings;

namespace Kudu.SiteManagement.Test.Configuration.Fakes
{
    public class ServiceBindingConfigurationElementFake : ServiceBindingConfigurationElement
    {
        public ServiceBindingConfigurationElementFake SetFake(string key, object value)
        {
            this[key] = value;
            return this;
        }
    }
}