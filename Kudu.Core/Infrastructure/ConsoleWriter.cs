using System;

namespace Kudu.Core.Infrastructure
{
    internal class ConsoleWriter : IWriter
    {
        public event Action BeforeWrite;

        public void WriteOutLine(string value)
        {
            if (BeforeWrite != null)
            {
                BeforeWrite();
            }

            Console.Out.WriteLine(value);
        }

        public void WriteErrorLine(string value)
        {
            if (BeforeWrite != null)
            {
                BeforeWrite();
            }

            Console.Error.WriteLine(value);
        }
    }
}
