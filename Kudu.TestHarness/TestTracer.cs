using System;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace Kudu.TestHarness
{
    public static class TestTracer
    {
        public const string DataSlotName = "TraceOutput";

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

            message = String.Format(CultureInfo.CurrentCulture, "{0}Z {1}", messageDateTime.ToUniversalTime().ToString("s"), message.Replace("\n", "\n\t"));

            var strb = (StringBuilder)CallContext.LogicalGetData(TestTracer.DataSlotName);
            if (strb != null)
            {
                lock (strb)
                {
                    strb.AppendLine(message);
                }
            }
            else
            {
                System.Diagnostics.Trace.WriteLine(message);
            }
        }

        public static void InitializeContext()
        {
            CallContext.LogicalSetData(TestTracer.DataSlotName, new StringBuilder());
        }

        public static void FreeContext()
        {
            CallContext.FreeNamedDataSlot(TestTracer.DataSlotName);
        }

        public static string GetTraceString()
        {
            var strb = (StringBuilder)CallContext.LogicalGetData(TestTracer.DataSlotName);
            if (strb != null)
            {
                lock (strb)
                {
                    return strb.ToString();
                }
            }

            return String.Empty;
        }

        public static void Trace(string messageFormat, params object[] args)
        {
            Trace(DateTime.UtcNow, messageFormat, args);
        }

        public static void TraceDeploymentLog(ApplicationManager appManager, string id)
        {
            Trace("\n====================================================================================\n\t\tDeployment Log for " + id + "\n=====================================================================================");

            var entries = appManager.DeploymentManager.GetLogEntriesAsync(id).Result.ToList();
            var allDetails = entries.Where(e => e.DetailsUrl != null)
                                    .AsParallel()
                                    .SelectMany(e => appManager.DeploymentManager.GetLogEntryDetailsAsync(id, e.Id).Result)
                                    .ToList();
            var allEntries = entries.Concat(allDetails);
            foreach (var entry in allEntries)
            {
                Trace(entry.LogTime, entry.Message);
            }
        }
    }
}
