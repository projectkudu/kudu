using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Stress
{
    class StressConsoleTraceListener : ConsoleTraceListener 
    {
        bool filterForStress = false; 
        public StressConsoleTraceListener(bool filterForStress)
        {
            this.filterForStress = filterForStress;
        }

        public override void WriteLine(string message)
        {
            if (filterForStress && !message.StartsWith("Stress"))
            {
                return;
            }
            base.WriteLine(message);
        }

        public override void WriteLine(string message, string category)
        {
            if (filterForStress && category != "Stress")
            {
                return;
            }
            base.WriteLine(message, category);
        }
    }

    class StressTextWriterTraceListener : TextWriterTraceListener
    {
        bool filterForStress = false;
        public StressTextWriterTraceListener(string fileName, bool filterForStress)
            : base(fileName)
        {
            this.filterForStress = filterForStress;
        }

        public StressTextWriterTraceListener(TextWriter stream, bool filterForStress)
            : base(stream)
        {
            this.filterForStress = filterForStress;
        }

        public override void WriteLine(string message)
        {
            if (filterForStress && !message.StartsWith("Stress"))
            {
                return;
            }
            base.WriteLine(message);
        }

        public override void WriteLine(string message, string category)
        {
            if (filterForStress && category != "Stress")
            {
                return;
            }
            base.WriteLine(message, category);
        }
    }

}
