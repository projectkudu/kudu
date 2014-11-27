using Kudu.SiteManagement.Configuration.Section.Bindings;

namespace Kudu.SiteManagement.Test.Configuration.Fakes
{
    public class ApplicationBindingConfigurationElementFake : ApplicationBindingConfigurationElement
    {
        public ApplicationBindingConfigurationElementFake SetFake(string key, object value)
        {
            this[key] = value;
            return this;
        }
    }
}