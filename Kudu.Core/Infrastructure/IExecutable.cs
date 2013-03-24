using System;
using System.Collections.Generic;
using System.Text;

namespace Kudu.Core.Infrastructure
{
    public interface IExecutable
    {
        string WorkingDirectory { get; }

        string Path { get; }

        IDictionary<string, string> EnvironmentVariables { get; }

        Encoding Encoding { get; set; }

        TimeSpan IdleTimeout { get; }

        void SetHomePath(string homePath);

#if SITEMANAGEMENT
        Tuple<string, string> Execute(string arguments, params object[] args); 
#else
        Tuple<string, string> Execute(Kudu.Contracts.Tracing.ITracer tracer, string arguments, params object[] args);
#endif

    }
}
