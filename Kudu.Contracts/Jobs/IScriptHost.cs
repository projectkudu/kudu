using System.Collections.Generic;

namespace Kudu.Contracts.Jobs
{
    public interface IScriptHost
    {
        string HostPath { get; }

        string ArgumentsFormat { get; }

        IEnumerable<string> SupportedExtensions { get; }
        IEnumerable<string> SupportedFileNames { get; }
    }
}