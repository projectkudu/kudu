using System.Collections.Generic;

namespace Kudu.Web {
    /// <summary>
    /// Represents a project
    /// </summary>
    public class Project {        
        /// <summary>
        /// Name of the project
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Files in the project
        /// </summary>
        public IEnumerable<File> Files { get; set; }
    }
}