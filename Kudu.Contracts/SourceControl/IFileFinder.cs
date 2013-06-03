using System.Collections.Generic;
using System.IO;

namespace Kudu.Core.SourceControl
{
    public interface IFileFinder
    {
        IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList);
    }
}
