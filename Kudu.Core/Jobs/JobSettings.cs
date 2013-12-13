using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;
using Newtonsoft.Json;

namespace Kudu.Core.Jobs
{
    public class JobSettings
    {
        public static JobSettings LoadJobSettings(string jobSettingsDirectory, IFileSystem fileSystem, ITraceFactory traceFactory)
        {
            if (jobSettingsDirectory == null || fileSystem == null || traceFactory == null)
            {
                throw new InvalidOperationException("Invalid input");
            }

            try
            {
                string jobSettingsPath = Path.Combine(jobSettingsDirectory, "job.settings.json");
                if (fileSystem.File.Exists(jobSettingsPath))
                {
                    string jobSettingsContent = fileSystem.File.ReadAllText(jobSettingsPath);
                    return JsonConvert.DeserializeObject<JobSettings>(jobSettingsContent);
                }
            }
            catch (Exception ex)
            {
                traceFactory.GetTracer().TraceError(ex);
            }

            return new JobSettings();
        }

        [JsonProperty("extra_info_url_template")]
        public string ExtraInfoUrlTemplate { get; set; }
    }
}