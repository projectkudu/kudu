using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace Kudu.Client.Models {
    public class Application: IApplication {
        public string Name { get; set; }
        public string ServiceUrl { get; set; }
        public string SiteUrl { get; set; }
    }
}
