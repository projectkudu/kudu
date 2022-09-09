using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kudu.Contracts.Scan
{
    public class ScanDetail
    {
        [JsonProperty(PropertyName = "total_scanned")]
        public String TotalScanned { get; set; }

        [JsonProperty(PropertyName = "total_infected")]
        public String TotalInfected { get; set; }

        [JsonProperty(PropertyName = "time_taken")]
        public String TimeTaken { get; set; }

        [JsonProperty(PropertyName = "safe_files")]
        public List<String> SafeFiles { get; set; }

        [JsonProperty(PropertyName = "infected_files")]
        public List<InfectedFileObject> InfectedFiles { get; set; }
    }
}
