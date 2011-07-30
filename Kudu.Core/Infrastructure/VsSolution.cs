using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Kudu.Core.Infrastructure {
    public class VsSolution {
        // internal class SolutionParser
        // Name: Microsoft.Build.Construction.SolutionParser
        // Assembly: Microsoft.Build, Version=4.0.0.0
        private const string SolutionParserTypeName = "Microsoft.Build.Construction.SolutionParser, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

        private static readonly Type _solutionParser;
        private static readonly PropertyInfo solutionReaderProperty;
        private static readonly MethodInfo parseSolutionMethod;
        private static readonly PropertyInfo projectsProperty;

        static VsSolution() {
            _solutionParser = Type.GetType(SolutionParserTypeName, throwOnError: false, ignoreCase: false);

            if (_solutionParser != null) {
                solutionReaderProperty = ReflectionUtility.GetInternalProperty(_solutionParser, "SolutionReader");
                projectsProperty = ReflectionUtility.GetInternalProperty(_solutionParser, "Projects");
                parseSolutionMethod = ReflectionUtility.GetInternalMethod(_solutionParser, "ParseSolution");
            }
        }

        public string Path { get; set; }
        public IEnumerable<VsSolutionProject> Projects { get; private set; }

        public VsSolution(string solutionPath) {
            if (_solutionParser == null) {
                throw new InvalidOperationException("Can not find type 'Microsoft.Build.Construction.SolutionParser' are you missing a assembly reference to 'Microsoft.Build.dll'?");
            }

            var solutionParser = GetSolutionParserInstance();

            using (var streamReader = new StreamReader(solutionPath)) {
                solutionReaderProperty.SetValue(solutionParser, streamReader);
                parseSolutionMethod.Invoke(solutionParser, null);
            }

            var projects = new List<VsSolutionProject>();
            var projectsArray = projectsProperty.GetValue<object[]>(solutionParser);

            foreach (var project in projectsArray) {
                projects.Add(new VsSolutionProject(solutionPath, project));
            }
            
            Path = solutionPath;
            Projects = projects;
        }

        private static object GetSolutionParserInstance() {
            // Get the constructor for Solution parser
            var ctor = _solutionParser.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).First();

            // Create an instance of the solution parser
            return ctor.Invoke(null);
        }

        public static IEnumerable<VsSolution> GetSolutions(string path) {
            return from solutionFile in Directory.EnumerateFiles(path, "*.sln", SearchOption.AllDirectories)
                   select new VsSolution(solutionFile);
        }
    }
}
