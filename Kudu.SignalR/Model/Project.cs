using System.Collections.Generic;

namespace Kudu.SignalR.Model {
    /// <summary>
    /// Represents a project
    /// </summary>
    public class Project {
        /// <summary>
        /// Name of the project
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Default project
        /// </summary>
        public string DefaultProject { get; set; }

        public IEnumerable<string> Projects { get; set; }

        /// <summary>
        /// Files in the project
        /// </summary>
        public IEnumerable<ProjectFile> Files { get; set; }
    }
}