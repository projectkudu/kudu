using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Kudu.Core.Infrastructure
{
    [DebuggerDisplay("{ProjectName}")]
    public class VsSolutionProject
    {
        private const string ProjectInSolutionTypeName = "Microsoft.Build.Construction.ProjectInSolution, Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

        private static readonly Type _projectInSolutionType;
        private static readonly PropertyInfo _projectNameProperty;
        private static readonly PropertyInfo _relativePathProperty;
        private static readonly PropertyInfo _projectTypeProperty;
        private static readonly PropertyInfo _projectExtensionProperty;
        private static readonly PropertyInfo _aspNetConfigurationsProperty;

        static VsSolutionProject()
        {
            _projectInSolutionType = Type.GetType(ProjectInSolutionTypeName, throwOnError: false, ignoreCase: false);

            if (_projectInSolutionType != null)
            {
                _projectNameProperty = ReflectionUtility.GetInternalProperty(_projectInSolutionType, "ProjectName");
                _relativePathProperty = ReflectionUtility.GetInternalProperty(_projectInSolutionType, "RelativePath");
                _projectTypeProperty = ReflectionUtility.GetInternalProperty(_projectInSolutionType, "ProjectType");
                _projectExtensionProperty = ReflectionUtility.GetInternalProperty(_projectInSolutionType, "Extension");
                _aspNetConfigurationsProperty = ReflectionUtility.GetInternalProperty(_projectInSolutionType, "AspNetConfigurations");
            }
        }

        private readonly string _solutionPath;
        private readonly object _projectInstance;

        private bool _isWap;
        private bool _isWebSite;
        private bool _isExecutable;
        private bool _isAspNet5;
        private IEnumerable<Guid> _projectTypeGuids;
        private string _projectName;
        private string _absolutePath;

        private bool _initialized;

        public IEnumerable<Guid> ProjectTypeGuids
        {
            get
            {
                EnsureProperties();
                return _projectTypeGuids;
            }
        }

        public string ProjectName
        {
            get
            {
                EnsureProperties();
                return _projectName;
            }
        }

        public string AbsolutePath
        {
            get
            {
                EnsureProperties();
                return _absolutePath;
            }
        }

        public bool IsWebSite
        {
            get
            {
                EnsureProperties();
                return _isWebSite;
            }
        }

        public bool IsWap
        {
            get
            {
                EnsureProperties();
                return _isWap;
            }
        }

        public bool IsExecutable
        {
            get
            {
                EnsureProperties();
                return _isExecutable;
            }
        }

        public bool IsAspNet5
        {
            get
            {
                EnsureProperties();
                return _isAspNet5;
            }
        }

        public VsSolutionProject(string solutionPath, object project)
        {
            _solutionPath = solutionPath;
            _projectInstance = project;
        }

        private void EnsureProperties()
        {
            if (_initialized)
            {
                return;
            }

            _projectName = _projectNameProperty.GetValue<string>(_projectInstance);
            var projectType = _projectTypeProperty.GetValue<SolutionProjectType>(_projectInstance);
            var projectExtension = _projectExtensionProperty.GetValue<string>(_projectInstance);
            var relativePath = _relativePathProperty.GetValue<string>(_projectInstance);
            _isWebSite = projectType == SolutionProjectType.WebProject;

            // When using websites with IISExpress, the relative path property becomes a URL.
            // When that happens we're going to grab the path from the Release.AspNetCompiler.PhysicalPath
            // property in the solution.

            Uri uri;
            if (_isWebSite && Uri.TryCreate(relativePath, UriKind.Absolute, out uri))
            {
                var aspNetConfigurations = _aspNetConfigurationsProperty.GetValue<Hashtable>(_projectInstance);

                // Use the release configuraiton and debug if it isn't available
                object configurationObject = aspNetConfigurations["Release"] ?? aspNetConfigurations["Debug"];

                // REVIEW: Is there always a configuration object (i.e. can this ever be null?)

                // The aspNetPhysicalPath contains the relative to the website
                FieldInfo aspNetPhysicalPathField = configurationObject.GetType().GetField("aspNetPhysicalPath", BindingFlags.NonPublic | BindingFlags.Instance);

                relativePath = (string)aspNetPhysicalPathField.GetValue(configurationObject);
            }

            _absolutePath = Path.Combine(Path.GetDirectoryName(_solutionPath), relativePath);

            if (projectType == SolutionProjectType.KnownToBeMSBuildFormat && File.Exists(_absolutePath))
            {
                // If the project is an msbuild project then extra the project type guids
                _projectTypeGuids = VsHelper.GetProjectTypeGuids(_absolutePath);

                // Check if it's a wap
                _isWap = VsHelper.IsWap(_projectTypeGuids);

                _isExecutable = VsHelper.IsExecutableProject(_absolutePath);
            }
            else if (projectExtension.Equals(".xproj", StringComparison.OrdinalIgnoreCase) && File.Exists(_absolutePath))
            {
                var projectPath = Path.Combine(Path.GetDirectoryName(_absolutePath), "project.json");
                if (AspNet5Helper.IsWebApplicationProjectJsonFile(projectPath))
                {
                    _isAspNet5 = true;
                    _absolutePath = projectPath;
                }
                _projectTypeGuids = Enumerable.Empty<Guid>();
            }
            else
            {
                _projectTypeGuids = Enumerable.Empty<Guid>();
            }

            _initialized = true;
        }

        // Microsoft.Build.Construction.SolutionProjectType
        private enum SolutionProjectType
        {
            Unknown,
            KnownToBeMSBuildFormat,
            SolutionFolder,
            WebProject,
            WebDeploymentProject,
            EtpSubProject,
        }
    }
}
