using System.Collections.Generic;
using System.Linq;
using Kudu.Core.Editor;

namespace Kudu.SignalR.ViewModels
{
    public class ProjectViewModel
    {
        public ProjectViewModel(string name, Project project)
        {
            Name = name;
            Projects = project.ProjectFiles;
            Files = project.Files.Select(path => new ProjectFile { Path = path });
            Solutions = project.SolutionFiles;
        }


        public string Name { get; set; }

        public string DefaultSolution
        {
            get
            {
                return Solutions.FirstOrDefault();
            }
        }

        public IEnumerable<string> Projects { get; set; }

        public IEnumerable<string> Solutions { get; set; }

        public IEnumerable<ProjectFile> Files { get; set; }
    }
}