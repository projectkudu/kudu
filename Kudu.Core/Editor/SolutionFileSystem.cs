using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Kudu.Core.Editor {
    public class SolutionFileSystem : PhysicalFileSystem {
        private readonly string _path;

        public SolutionFileSystem(string path)
            : base(path) {
            _path = path;
        }

        public override IEnumerable<string> GetFiles() {
            // Get all project files under the root
            var projects = from file in Directory.GetFiles(_path, "*proj", SearchOption.AllDirectories)
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