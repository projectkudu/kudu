using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Kudu.Core.Editor {
    public class SolutionFileSystem : PhysicalFileSystem {
        private readonly IEnumerable<string> _solutionFiles;

        public SolutionFileSystem(string path, IEnumerable<string> solutionFiles)
            : base(path) {
            _solutionFiles = solutionFiles;
        }

        public override IEnumerable<string> GetFiles() {            
            return from solutionFile in _solutionFiles
                   from file in GetSolutionFiles(Path.GetDirectoryName(solutionFile))
                   select file;
        }

        private IEnumerable<string> GetSolutionFiles(string path) {
            // Get all project files under the root
            var projects = from file in Directory.GetFiles(path, "*proj", SearchOption.AllDirectories)
                           select new {
                               Document = XDocument.Parse(File.ReadAllText(file)),
                               Path = file
                           };

            var files = (from project in projects
                         from itemGroup in project.Document.Root.Elements(GetName("ItemGroup"))
                         let contentFiles = itemGroup.Elements(GetName("Content"))
                         let compileFiles = itemGroup.Elements(GetName("Compile"))
                         let allFiles = contentFiles.Concat(compileFiles)
                         from file in allFiles
                         let relativePath = file.Attribute("Include").Value
                         let projectDir = Path.GetDirectoryName(project.Path)
                         let fullPath = Path.Combine(projectDir, relativePath)
                         select fullPath).ToList();

            return files.Select(MakeRelative);
        }

        private static XName GetName(string name) {
            return XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");
        }
    }
}