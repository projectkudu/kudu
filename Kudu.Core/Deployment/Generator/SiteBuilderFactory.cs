using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;

namespace Kudu.Core.Deployment.Generator
{
    public class SiteBuilderFactory : ISiteBuilderFactory
    {
        private readonly IEnvironment _environment;
        private readonly IBuildPropertyProvider _propertyProvider;

        public SiteBuilderFactory(IBuildPropertyProvider propertyProvider, IEnvironment environment)
        {
            _propertyProvider = propertyProvider;
            _environment = environment;
        }

        public ISiteBuilder CreateBuilder(ITracer tracer, ILogger logger, IDeploymentSettingsManager settings, IFileFinder fileFinder)
        {
            string repositoryRoot = _environment.RepositoryPath;

            // Use the cached vs projects file finder for: a. better performance, b. ignoring solutions/projects under node_modules
            fileFinder = new CachedVsProjectsFileFinder(fileFinder);

            // If there's a custom deployment file then let that take over.
            var command = settings.GetValue(SettingsKeys.Command);
            if (!String.IsNullOrEmpty(command))
            {
                return new CustomBuilder(_environment, settings, _propertyProvider, repositoryRoot, command);
            }

            // If the user provided specific generator arguments, that overrides any detection logic
            string scriptGeneratorArgs = settings.GetValue(SettingsKeys.ScriptGeneratorArgs);
            if (!String.IsNullOrEmpty(scriptGeneratorArgs))
            {
                return new CustomGeneratorCommandSiteBuilder(_environment, settings, _propertyProvider, repositoryRoot, scriptGeneratorArgs);
            }

            // If the repository has an explicit pointer to a project path to be deployed
            // then use it.
            string targetProjectPath = settings.GetValue(SettingsKeys.Project);
            if (!String.IsNullOrEmpty(targetProjectPath))
            {
                tracer.Trace("Specific project was specified: " + targetProjectPath);

                targetProjectPath = Path.GetFullPath(Path.Combine(repositoryRoot, targetProjectPath.TrimStart('/', '\\')));

                // Try to resolve the project
                return ResolveProject(repositoryRoot,
                                      targetProjectPath,
                                      settings,
                                      fileFinder,
                                      tryWebSiteProject: true,
                                      searchOption: SearchOption.TopDirectoryOnly);
            }

            // Get all solutions in the current repository path
            var solutions = VsHelper.GetSolutions(repositoryRoot, fileFinder).ToList();

            if (!solutions.Any())
            {
                return ResolveProject(repositoryRoot,
                                      settings,
                                      fileFinder,
                                      searchOption: SearchOption.AllDirectories);
            }

            // More than one solution is ambiguous
            if (solutions.Count > 1)
            {
                // TODO: Show relative paths in error messages
                ThrowAmbiguousSolutionsError(solutions);
            }

            // We have a solution
            VsSolution solution = solutions[0];

            // We need to determine what project to deploy so get a list of all web projects and
            // figure out with some heuristic, which one to deploy.

            // TODO: Pick only 1 and throw if there's more than one
            VsSolutionProject project = solution.Projects.Where(p => p.IsWap || p.IsWebSite || p.IsAspNet5).FirstOrDefault();

            if (project == null)
            {
                // Try executable type project
                project = solution.Projects.Where(p => p.IsExecutable).FirstOrDefault();
                if (project != null)
                {
                    return new DotNetConsoleBuilder(_environment,
                                              settings,
                                              _propertyProvider,
                                              repositoryRoot,
                                              project.AbsolutePath,
                                              solution.Path);
                }

                logger.Log(Resources.Log_NoDeployableProjects, solution.Path);

                return ResolveNonAspProject(repositoryRoot, null, settings);
            }

            if (project.IsWap)
            {
                return new WapBuilder(_environment,
                                      settings,
                                      _propertyProvider,
                                      repositoryRoot,
                                      project.AbsolutePath,
                                      solution.Path);
            }

            if (project.IsAspNet5)
            {
                return new AspNet5Builder(_environment,
                                      settings,
                                      _propertyProvider,
                                      fileFinder,
                                      repositoryRoot,
                                      project.AbsolutePath,
                                      isConsoleApp: false);
            }

            return new WebSiteBuilder(_environment,
                                      settings,
                                      _propertyProvider,
                                      repositoryRoot,
                                      project.AbsolutePath,
                                      solution.Path);
        }

        private ISiteBuilder ResolveNonAspProject(string repositoryRoot, string projectPath, IDeploymentSettingsManager perDeploymentSettings)
        {
            string sourceProjectPath = projectPath ?? repositoryRoot;
            if (IsNodeSite(sourceProjectPath))
            {
                return new NodeSiteBuilder(_environment, perDeploymentSettings, _propertyProvider, repositoryRoot, projectPath);
            }
            else if (IsPythonSite(sourceProjectPath))
            {
                return new PythonSiteBuilder(_environment, perDeploymentSettings, _propertyProvider, repositoryRoot, projectPath);
            }
            else if (IsGoSite(sourceProjectPath))
            {
                return new GoSiteBuilder(_environment, perDeploymentSettings, _propertyProvider, repositoryRoot, projectPath);
            }

            return new BasicBuilder(_environment, perDeploymentSettings, _propertyProvider, repositoryRoot, projectPath);
        }

        private static bool IsGoSite(string projectPath)
        {
            return GoSiteEnabler.LooksLikeGo(projectPath);
        }

        private static bool IsNodeSite(string projectPath)
        {
            return NodeSiteEnabler.LooksLikeNode(projectPath);
        }

        private static bool IsPythonSite(string projectPath)
        {
            return PythonSiteEnabler.LooksLikePython(projectPath);
        }

        private ISiteBuilder ResolveProject(string repositoryRoot, IDeploymentSettingsManager perDeploymentSettings, IFileFinder fileFinder, bool tryWebSiteProject = false, SearchOption searchOption = SearchOption.AllDirectories)
        {
            return ResolveProject(repositoryRoot, repositoryRoot, perDeploymentSettings, fileFinder, tryWebSiteProject, searchOption, specificConfiguration: false);
        }

        private ISiteBuilder ResolveProject(string repositoryRoot, string targetPath, IDeploymentSettingsManager perDeploymentSettings, IFileFinder fileFinder, bool tryWebSiteProject, SearchOption searchOption = SearchOption.AllDirectories, bool specificConfiguration = true)
        {
            if (DeploymentHelper.IsProject(targetPath))
            {
                return DetermineProject(repositoryRoot, targetPath, perDeploymentSettings, fileFinder);
            }

            // Check for loose projects
            var projects = DeploymentHelper.GetProjects(targetPath, fileFinder, searchOption);
            if (projects.Count > 1)
            {
                // Can't determine which project to build
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_AmbiguousProjects,
                                                                  String.Join(", ", projects)));
            }
            else if (projects.Count == 1)
            {
                return DetermineProject(repositoryRoot, projects[0], perDeploymentSettings, fileFinder);
            }
            
            // Check for ASP.NET 5 project without VS solution or project
            string projectJson;
            if (AspNet5Helper.TryAspNet5WebProject(targetPath, out projectJson))
            {
                return new AspNet5Builder(_environment,
                                           perDeploymentSettings,
                                           _propertyProvider,
                                           fileFinder,
                                           repositoryRoot,
                                           projectJson,
                                           isConsoleApp: false);
            }

            if (AspNet5Helper.TryAspNet5ConsoleAppProject(targetPath, out projectJson))
            {
                return new AspNet5Builder(_environment,
                                           perDeploymentSettings,
                                           _propertyProvider,
                                           fileFinder,
                                           repositoryRoot,
                                           projectJson,
                                           isConsoleApp: true);
            }

            if (tryWebSiteProject)
            {
                // Website projects need a solution to build so look for one in the repository path
                // that has this website in it.
                var solutions = VsHelper.FindContainingSolutions(repositoryRoot, targetPath, fileFinder);

                // More than one solution is ambiguous
                if (solutions.Count > 1)
                {
                    ThrowAmbiguousSolutionsError(solutions);
                }
                else if (solutions.Count == 1)
                {
                    // Unambiguously pick the root
                    return new WebSiteBuilder(_environment,
                                              perDeploymentSettings,
                                              _propertyProvider,
                                              repositoryRoot,
                                              targetPath,
                                              solutions[0].Path);
                }
            }

            // This should only ever happen if the user specifies an invalid directory.
            // The other case where the method is called we always resolve the path so it's a non issue there.
            if (specificConfiguration && !Directory.Exists(targetPath))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_ProjectDoesNotExist,
                                                                  targetPath));
            }

            // If there's none then use the basic builder (the site is xcopy deployable)
            return ResolveNonAspProject(repositoryRoot, targetPath, perDeploymentSettings);
        }

        private ISiteBuilder DetermineProject(string repositoryRoot, string targetPath, IDeploymentSettingsManager perDeploymentSettings, IFileFinder fileFinder)
        {
            if (!DeploymentHelper.IsDeployableProject(targetPath))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_ProjectNotDeployable,
                                                                  targetPath));
            }
            else if (File.Exists(targetPath)) // TODO: what is this if about?
            {
                var solution = VsHelper.FindContainingSolution(repositoryRoot, targetPath, fileFinder);
                string solutionPath = solution != null ? solution.Path : null;

                if (VsHelper.IsWap(targetPath))
                {
                    return new WapBuilder(_environment,
                                          perDeploymentSettings,
                                          _propertyProvider,
                                          repositoryRoot,
                                          targetPath,
                                          solutionPath);
                }
                else
                {
                    // This is a console app
                    return new DotNetConsoleBuilder(_environment,
                                          perDeploymentSettings,
                                          _propertyProvider,
                                          repositoryRoot,
                                          targetPath,
                                          solutionPath);
                }
            }

            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_ProjectDoesNotExist,
                                                                  targetPath));
        }

        private static void ThrowAmbiguousSolutionsError(IList<VsSolution> solutions)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                              Resources.Error_AmbiguousSolutions,
                                                              String.Join(", ", solutions.Select(s => s.Path))));
        }

        private class CachedVsProjectsFileFinder : IFileFinder
        {
            // The way CachedVsProjectsFileFinder works is it looks up all the files in this CachedExtensions list the first time
            // then it's the looked up list is filtered with the passed in lookupList to ListFiles()
            private static readonly string[] CachedExtensions = DeploymentHelper.ProjectFileLookup.Concat(VsHelper.SolutionsLookupList).Concat(AspNet5Helper.GlobalJsonLookupList).ToArray();

            private const string NodeModulesDirectory = "\\node_modules\\";

            private IFileFinder _fileFinder;
            private string _path;
            private List<string> _cachedResults;

            public CachedVsProjectsFileFinder(IFileFinder fileFinder)
            {
                _fileFinder = fileFinder;
            }

            public IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList)
            {
                if (searchOption == SearchOption.AllDirectories && (_path == null || PathUtilityFactory.Instance.PathsEquals(_path, path)))
                {
                    if (_cachedResults == null)
                    {
                        _path = path;
                        _cachedResults =
                            _fileFinder.ListFiles(path, searchOption, CachedExtensions)
                                       .Where(filePath => !filePath.Contains(NodeModulesDirectory))
                                       .ToList();
                    }

                    lookupList = lookupList.Select(l => l.TrimStart('*')).ToArray();

                    return _cachedResults.Where(filePath => lookupList.Any(lookup => filePath.EndsWith(lookup, StringComparison.OrdinalIgnoreCase)));
                }

                return _fileFinder.ListFiles(path, searchOption, lookupList);
            }
        }
    }
}
