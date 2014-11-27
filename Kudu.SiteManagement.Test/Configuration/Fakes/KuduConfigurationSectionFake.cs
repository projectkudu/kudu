using Kudu.SiteManagement.Configuration.Section;

namespace Kudu.SiteManagement.Test.Configuration.Fakes
{
    public class KuduConfigurationSectionFake : KuduConfigurationSection
    {
        public KuduConfigurationSectionFake SetFake(string key, object value)
        {
            this[key] = value;
            return this;
        }
    }
}