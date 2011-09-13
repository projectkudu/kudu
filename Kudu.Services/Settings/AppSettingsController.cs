using XmlSettings;

namespace Kudu.Services.Settings {
    public class AppSettingsController : SettingsController {
        public AppSettingsController(ISettings settings)
            : base("appSettings", settings) {
        }
    }
}
