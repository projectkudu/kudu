using System.Collections.Generic;
using System.Web.Mvc;
using Kudu.Services.Infrastructure;
using XmlSettings;
using System.Linq;

namespace Kudu.Services.Settings {
    public abstract class SettingsController : KuduController {
        private readonly ISettings _settings;
        private readonly string _section;

        public SettingsController(string section, ISettings settings) {
            _section = section;
            _settings = settings;
        }

        public IEnumerable<KeyValuePair<string, string>> Index() {
            return _settings.GetValues(_section) ?? 
                   Enumerable.Empty<KeyValuePair<string, string>>();
        }

        [HttpPost]
        public void Set(string key, string value) {
            _settings.SetValue(_section, key, value);
        }

        [HttpPost]
        public void Remove(string key) {
            _settings.DeleteValue(_section, key);
        }

        public string Get(string key) {
            return _settings.GetValue(_section, key);
        }
    }
}
