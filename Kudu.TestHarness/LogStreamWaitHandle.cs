using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.TestHarness
{
    public class LogStreamWaitHandle : IDisposable
    {
        Stream stream;
        List<string> lines;
        Semaphore sem;
        ManualResetEvent disposed = new ManualResetEvent(false);

        public LogStreamWaitHandle(Stream stream)
        {
            this.stream = stream;
            this.lines = new List<string>();
            this.sem = new Semaphore(0, Int32.MaxValue);
            Task.Factory.StartNew(() =>
            {
                try
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        bool initial = true;
                        while (!reader.EndOfStream)
                        {
                            string line = reader.ReadLine();
                            if (line != null)
                            {
                                if (initial)
                                {
                                    // accommodate for gap between first welcome and event hookup
                                    Thread.Sleep(1000);
                                    initial = false;
                                }

                                lock (lines)
                                {
                                    lines.Add(line);
                                    this.sem.Release();
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    disposed.Set();
                }
            });
        }

        public void Dispose()
        {
            this.stream.Close();
            this.disposed.WaitOne(10000);
        }

        public string WaitNextLine(int millisecs)
        {
            if (this.sem.WaitOne(millisecs))
            {
                lock (lines)
                {
                    string result = lines[0];
                    lines.RemoveAt(0);
                    return result;
                }
            }

            return null;
        }
    }
}
