using System;
using System.Globalization;
using System.Linq;

namespace Kudu.TestHarness
{
    public static class TestTracer
    {
        public static void Trace(DateTime messageDateTime, string messageFormat, params object[] args)
        {
            string message;
            if (args != null && args.Length > 0)
            {
                message = String.Format(CultureInfo.CurrentCulture, messageFormat, args);
            }
            else
            {
                message = messageFormat;
            }
            System.Diagnostics.Trace.WriteLine(String.Format(CultureInfo.CurrentCulture, "{0}: {1}", messageDateTime, message));
        }

        public static void Trace(string messageFormat, params object[] args)
        {
            Trace(DateTime.Now, messageFormat, args);
        }

        public static void TraceDeploymentLog(ApplicationManager appManager, string id)
        {
            var entries = appManager.DeploymentManager.GetLogEntriesAsync(id).Result.ToList();
            var allDetails = entries.Where(e => e.DetailsUrl != null)
                                    .SelectMany(e => appManager.DeploymentManager.GetLogEntryDetailsAsync(id, e.Id).Result).ToList();
            var allEntries = entries.Concat(allDetails).ToList();
            foreach (var entry in allEntries)
            {
                var message = entry.Message;
                if (message != null)
                {
                    message = message.Replace("\n", "\n\t");
                }
                Trace(entry.LogTime, entry.Message);
            }
        }
    }
}
