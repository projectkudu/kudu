using Kudu.SiteManagement.Configuration.Section.Bindings;

namespace Kudu.SiteManagement.Test.Configuration.Fakes
{
    public class BindingsConfigurationElementCollectionFake : BindingsConfigurationElementCollection
    {
        public BindingsConfigurationElementCollectionFake AddFake(BindingConfigurationElement element)
        {
            base.Add(element);
            return this;
        }
    }
}