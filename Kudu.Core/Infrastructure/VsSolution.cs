using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Kudu.Core.Infrastructure
{
    public class VsSolution
    {
        // internal class SolutionParser
        // Name: Microsoft.Build.Construction.SolutionParser
        // Assembly: Microsoft.Build, Version=4.0.0.0
        private const string SolutionParserTypeName = "Microsoft.Build.Construction.SolutionParser, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

        private static readonly Type _solutionParser;
        private static readonly PropertyInfo _solutionReaderProperty;
        private static readonly MethodInfo _parseSolutionMethod;
        private static readonly PropertyInfo _projectsProperty;

        static VsSolution()
        {
            _solutionParser = Type.GetType(SolutionParserTypeName, throwOnError: false, ignoreCase: false);

            if (_solutionParser != null)
            {
                _solutionReaderProperty = ReflectionUtility.GetInternalProperty(_solutionParser, "SolutionReader");
                _projectsProperty = ReflectionUtility.GetInternalProperty(_solutionParser, "Projects");
                _parseSolutionMethod = ReflectionUtility.GetInternalMethod(_solutionParser, "ParseSolution", Type.EmptyTypes);
            }
        }

        private IEnumerable<VsSolutionProject> _projects;

        public string Path { get; private set; }
        public IEnumerable<VsSolutionProject> Projects
        {
            get
            {
                EnsureProjects();
                return _projects;
            }
        }

        public VsSolution(string solutionPath)
        {
            Debug.Assert(_solutionParser != null, "Can not find type 'Microsoft.Build.Construction.SolutionParser' are you missing a assembly reference to 'Microsoft.Build.dll'?");

            Path = solutionPath;
        }

        private void EnsureProjects()
        {
            if (_projects != null)
            {
                return;
            }

            var solutionParser = GetSolutionParserInstance();

            using (var streamReader = new StreamReader(Path))
            {
                _solutionReaderProperty.SetValue(solutionParser, streamReader);
                _parseSolutionMethod.Invoke(solutionParser, null);
            }

            var projects = new List<VsSolutionProject>();
            var projectsArray = _projectsProperty.GetValue<object[]>(solutionParser);

            foreach (var project in projectsArray)
            {
                projects.Add(new VsSolutionProject(Path, project));
            }

            _projects = projects;
        }

        private static object GetSolutionParserInstance()
        {
            // Get the constructor for Solution parser
            var ctor = _solutionParser.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null);

            // Create an instance of the solution parser
            return ctor.Invoke(null);
        }
    }
}