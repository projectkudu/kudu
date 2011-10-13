using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Web;
using XmlSettings;

namespace Kudu.Services.Settings {
    [ServiceContract]
    public class ConnectionStringsController {
        public ConnectionStringsController(ISettings settings)
            : this("connectionStrings", settings) {
        }

        private readonly ISettings _settings;
        private readonly string _section;

        public ConnectionStringsController(string section, ISettings settings) {
            _section = section;
            _settings = settings;
        }

        [WebGet(UriTemplate = "")]
        public IEnumerable<KeyValuePair<string, string>> Index() {
            return _settings.GetValues(_section) ??
                   Enumerable.Empty<KeyValuePair<string, string>>();
        }

        [WebInvoke]
        public void Set(SimpleJson.JsonObject input) {
            _settings.SetValue(_section, (string)input["key"], (string)input["value"]);
        }

        [WebInvoke]
        public void Remove(SimpleJson.JsonObject input) {
            _settings.DeleteValue(_section, (string)input["key"]);
        }

        [WebGet(UriTemplate = "get?key={key}")]
        public string Get(string key) {
            return _settings.GetValue(_section, key);
        }
    }
}
