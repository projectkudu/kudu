using Kudu.SiteManagement.Configuration.Section;

namespace Kudu.SiteManagement.Test.Configuration.Fakes
{
    public class PathConfigurationElementFake : PathConfigurationElement
    {
        public static PathConfigurationElementFake Fake(string key, object value)
        {
            return new PathConfigurationElementFake().SetFake(key, value);
        }

        public PathConfigurationElementFake SetFake(string key, object value)
        {
            this[key] = value;
            return this;
        }
    }
}