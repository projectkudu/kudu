using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kudu.Core.Editor {
    public interface IFileSystem {
        string ReadAllText(string path);
        IEnumerable<string> GetFiles();
        void WriteAllText(string path, string content);
        void Delete(string path);
    }
}
