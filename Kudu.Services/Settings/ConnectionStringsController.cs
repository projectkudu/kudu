using XmlSettings;

namespace Kudu.Services.Settings {
    public class ConnectionStringsController : SettingsController {
        public ConnectionStringsController(ISettings settings)
            : base("connectionStrings", settings) {
        }
    }
}
