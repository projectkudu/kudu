using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.Administration;


namespace Kudu.Stress
{
    class W3wpResourceMonitor
    {
        string webAppName = null ;
        int w3wpPid = -1;
        Process w3wpProcess = null;
        DateTime startTime = DateTime.Now;

        public W3wpResourceMonitor (string iisApplicationName)
        {
            this.webAppName = iisApplicationName;
        }

        public void CheckAndLogResourceUsage()
        {
            try
            {
                if (w3wpPid < 0)
                {
                    w3wpPid = GetW3wpPid(this.webAppName);
                    string pidMsg = string.Format("Stress Monitoring Start.  Monitoring w3wp.exe process with PID:  {0}", w3wpPid);
                    Trace.WriteLine(pidMsg);
                }
                w3wpProcess = Process.GetProcessById(w3wpPid);

                TimeSpan elapsedTime = DateTime.Now - startTime;
                int memoryusage = (int)w3wpProcess.PrivateMemorySize64;
                int handleCount = (int)w3wpProcess.HandleCount;
                int threadCount = (int)w3wpProcess.Threads.Count;
                string logMsg = string.Format("Stress Process Monitor (time in secs/private bytes/handles/threads), {0}, {1}, {2}, {3}", (int)elapsedTime.TotalSeconds, memoryusage, handleCount, threadCount);
                Trace.WriteLine(logMsg);

                // resource consumption check
                int memorythreshold = 300000000;
                int handleCountThreshold = 1500;
                int threadCountThreshold = 100;

                if (memoryusage > memorythreshold)
                {
                    throw new ApplicationException(string.Format("w3wp Counter Threshold Exceeded: Private Bytes:  {0} ,  Threshold: {1}", memoryusage, memorythreshold));
                }
                if (handleCount > handleCountThreshold)
                {
                    throw new ApplicationException(string.Format("w3wp Counter Threshold Exceeded: Handle Count:  {0} ,  Threshold: {1}", memoryusage, handleCountThreshold));
                }
                if (threadCount > threadCountThreshold)
                {
                    throw new ApplicationException(string.Format("w3wp Counter Threshold Exceeded: Thread Count:  {0} ,  Threshold: {1}", threadCount, threadCountThreshold));
                }
            }
            catch (Exception ex)
            {
                string msg = string.Format("Error accessing w3wp process monitoring info.  Exception:  " + ex.ToString());
                throw new ApplicationException(msg); 
            }
        }

        
        int GetW3wpPid(string appName)
        {
            ServerManager serverManager = new ServerManager () ;
            foreach (WorkerProcess workerProcess  in serverManager.WorkerProcesses)
            {
                if (workerProcess.AppPoolName.Contains(appName))
                {
                    return workerProcess.ProcessId ;
                }
            }
            throw new ApplicationException ("Error accessing Process Perfmon Counters for w3wp.exe:  No Worker Process found for Stress app");
        }



    }
}
