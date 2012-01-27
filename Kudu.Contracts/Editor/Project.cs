using System.Collections.Generic;

namespace Kudu.Core.Editor
{
    public class Project
    {
        public List<string> SolutionFiles { get; set; }
        public List<string> ProjectFiles { get; set; }
        public List<string> Files { get; set; }
    }
}