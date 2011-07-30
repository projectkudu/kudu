using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Kudu.Core.Infrastructure {
    public class VsSolution {
        //internal class SolutionParser
        //Name: Microsoft.Build.Construction.SolutionParser
        //Assembly: Microsoft.Build, Version=4.0.0.0

        private static readonly Type _solutionParser;
        private static readonly PropertyInfo _solutionParser_solutionReader;
        private static readonly MethodInfo _solutionParser_parseSolution;
        private static readonly PropertyInfo _solutionParser_projects;

        static VsSolution() {
            _solutionParser = Type.GetType("Microsoft.Build.Construction.SolutionParser, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", false, false);
            if (_solutionParser != null) {
                _solutionParser_solutionReader = _solutionParser.GetProperty("SolutionReader", BindingFlags.NonPublic | BindingFlags.Instance);
                _solutionParser_projects = _solutionParser.GetProperty("Projects", BindingFlags.NonPublic | BindingFlags.Instance);
                _solutionParser_parseSolution = _solutionParser.GetMethod("ParseSolution", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        public string Path { get; set; }
        public IEnumerable<VsSolutionProject> Projects { get; private set; }

        public VsSolution(string solutionPath) {
            if (_solutionParser == null) {
                throw new InvalidOperationException("Can not find type 'Microsoft.Build.Construction.SolutionParser' are you missing a assembly reference to 'Microsoft.Build.dll'?");
            }

            var solutionParser = _solutionParser.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).First().Invoke(null);
            using (var streamReader = new StreamReader(solutionPath)) {
                _solutionParser_solutionReader.SetValue(solutionParser, streamReader, null);
                _solutionParser_parseSolution.Invoke(solutionParser, null);
            }

            var projects = new List<VsSolutionProject>();
            var array = (Array)_solutionParser_projects.GetValue(solutionParser, null);
            for (int i = 0; i < array.Length; i++) {
                projects.Add(new VsSolutionProject(solutionPath, array.GetValue(i)));
            }

            Path = solutionPath;
            Projects = projects;
        }

        public static IEnumerable<VsSolution> GetSolutions(string path) {
            return from solutionFile in Directory.EnumerateFiles(path, "*.sln", SearchOption.AllDirectories)
                   select new VsSolution(solutionFile);
        }
    }    
}
