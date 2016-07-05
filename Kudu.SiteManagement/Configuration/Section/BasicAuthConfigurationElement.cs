using System.Configuration;

namespace Kudu.SiteManagement.Configuration.Section {

    public class BasicAuthConfigurationElement : ConfigurationElement {
        [ConfigurationProperty( "username" , IsRequired = true )]
        public string Username
        {
            get { return ( string ) this[ "username" ]; }           
        }


        [ConfigurationProperty( "password" , IsRequired = true )]
        public string Password
        {
            get { return ( string ) this[ "password" ]; }
        }

    }
}
