using System.Collections.Generic;

namespace Kudu.Core.Editor
{
    public class Project
    {
        public IEnumerable<string> SolutionFiles { get; set; }
        public IEnumerable<string> ProjectFiles { get; set; }
        public IEnumerable<string> Files { get; set; }
    }
}