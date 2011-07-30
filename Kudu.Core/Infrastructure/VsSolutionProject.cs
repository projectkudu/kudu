using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Kudu.Core.Infrastructure {
    [DebuggerDisplay("{ProjectName}")]
    public class VsSolutionProject {
        private static readonly Guid _wapGuid = new Guid("349c5851-65df-11da-9384-00065b846f21");

        private static readonly Type _projectInSolution;
        private static readonly PropertyInfo _projectInSolution_ProjectName;
        private static readonly PropertyInfo _projectInSolution_RelativePath;
        private static readonly PropertyInfo _projectInSolution_ProjectType;

        static VsSolutionProject() {
            _projectInSolution = Type.GetType("Microsoft.Build.Construction.ProjectInSolution, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", throwOnError: false, ignoreCase: false);
            if (_projectInSolution != null) {
                _projectInSolution_ProjectName = _projectInSolution.GetProperty("ProjectName", BindingFlags.NonPublic | BindingFlags.Instance);
                _projectInSolution_RelativePath = _projectInSolution.GetProperty("RelativePath", BindingFlags.NonPublic | BindingFlags.Instance);
                _projectInSolution_ProjectType = _projectInSolution.GetProperty("ProjectType", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        public string ProjectName { get; private set; }
        public string AbsolutePath { get; private set; }
        public bool IsWebSite { get; private set; }
        public bool IsWap { get; set; }

        public VsSolutionProject(string solutionPath, object solutionProject) {
            ProjectName = _projectInSolution_ProjectName.GetValue(solutionProject, null) as string;
            string relativePath = _projectInSolution_RelativePath.GetValue(solutionProject, null) as string;
            AbsolutePath = Path.Combine(Path.GetDirectoryName(solutionPath), relativePath);
            var projectType = (int)_projectInSolution_ProjectType.GetValue(solutionProject, null);
            IsWebSite = projectType == 3;
            IsWap = projectType == 1 && IsProjectFileWap(AbsolutePath);
        }


        private static bool IsProjectFileWap(string path) {
            var document = XDocument.Parse(File.ReadAllText(path));

            var guids = from propertyGroup in document.Root.Elements(GetName("PropertyGroup"))
                        let projectTypeGuids = propertyGroup.Element(GetName("ProjectTypeGuids"))
                        where projectTypeGuids != null
                        from guid in projectTypeGuids.Value.Split(';')
                        select new Guid(guid.Trim('{', '}'));

            return guids.Contains(_wapGuid);
        }

        private static XName GetName(string name) {
            return XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");
        }
    }
}
