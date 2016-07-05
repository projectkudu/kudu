using Kudu.SiteManagement.Configuration.Section;

namespace Kudu.SiteManagement.Test.Configuration.Fakes {
    public class BasicAuthConfigurationElementFake : BasicAuthConfigurationElement {
        public static BasicAuthConfigurationElementFake Fake( string username , string password ) {
            return new BasicAuthConfigurationElementFake( ).SetFake( username , password );
        }

        public BasicAuthConfigurationElementFake SetFake(string username , string password ) {
            this[ "username" ] = username;
            this[ "password" ] = password;
            return this;
        }
    }
}
