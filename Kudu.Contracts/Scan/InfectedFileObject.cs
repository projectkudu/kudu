using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kudu.Contracts.Scan
{
    public class InfectedFileObject
    {
        [JsonProperty(PropertyName = "name")]
        public String Name { get; set; }

        [JsonProperty(PropertyName = "threat_detected")]
        public String ThreatDetected { get; set; }

        public InfectedFileObject(string name, string threat)
        {
            this.Name = name;
            this.ThreatDetected = threat;
        }
    }
}
