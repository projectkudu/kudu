using System;
using System.Collections.Generic;
using System.Text;
using Kudu.Contracts.Tracing;

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

        Tuple<string, string> Execute(ITracer tracer, string arguments, params object[] args);
    }
}
