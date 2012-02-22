using System;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
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

        public ISiteBuilder CreateBuilder(ILogger logger)
        {
            string repositoryRoot = _environment.DeploymentRepositoryPath;

            var configuration = new DeploymentConfiguration(repositoryRoot);

            // If the repository has an explicit pointer to a project path to be deployed
            // then use it.
            string targetProjectPath = configuration.ProjectPath;
            if (!String.IsNullOrEmpty(targetProjectPath))
            {
                // Try to resolve the project
                return ResolveProject(repositoryRoot,
                                      targetProjectPath,
                                      tryWebSiteProject: true,
                                      searchOption: SearchOption.TopDirectoryOnly);
            }

            // Get all solutions in the current repository path
            var solutions = VsHelper.GetSolutions(repositoryRoot).ToList();

            if (!solutions.Any())
            {
                return ResolveProject(repositoryRoot,
                                      searchOption: SearchOption.AllDirectories);
            }

            // More than one solution is ambiguous
            if (solutions.Count > 1)
            {
                throw new InvalidOperationException("Unable to determine which solution file to build.");
            }

            // We have a solution
            VsSolution solution = solutions[0];

            // We need to determine what project to deploy so get a list of all web projects and
            // figure out with some heuristic, which one to deploy. 
            // For now just pick the first one we find.
            VsSolutionProject project = solution.Projects.Where(p => p.IsWap || p.IsWebSite).FirstOrDefault();

            if (project == null)
            {
                logger.Log("Found solution {0} with no deployable projects. Deploying files instead.", solution.Path);

                return new BasicBuilder(repositoryRoot, _environment.TempPath);
            }

            if (project.IsWap)
            {
                return new WapBuilder(_propertyProvider, 
                                      repositoryRoot, 
                                      project.AbsolutePath, 
                                      _environment.TempPath, 
                                      solution.Path);
            }

            return new WebSiteBuilder(_propertyProvider, 
                                      repositoryRoot, 
                                      solution.Path, 
                                      project.AbsolutePath);
        }

        private ISiteBuilder ResolveProject(string repositoryRoot, bool tryWebSiteProject = false, SearchOption searchOption = SearchOption.AllDirectories)
        {
            return ResolveProject(repositoryRoot, repositoryRoot, tryWebSiteProject, searchOption);
        }

        private ISiteBuilder ResolveProject(string repositoryRoot, string targetPath, bool tryWebSiteProject, SearchOption searchOption = SearchOption.AllDirectories)
        {
            if (File.Exists(targetPath) &&
                DeploymentHelper.IsDeployableProject(targetPath))
            {
                return new WapBuilder(_propertyProvider,
                                      repositoryRoot,
                                      targetPath,
                                      _environment.TempPath);
            }

            // Check for loose projects
            var projects = DeploymentHelper.GetDeployableProjects(targetPath, searchOption);
            if (projects.Count > 1)
            {
                // Can't determine which project to build
                throw new InvalidOperationException("Unable to determine which project file to build.");
            }
            else if (projects.Count == 1)
            {
                return new WapBuilder(_propertyProvider,
                                      repositoryRoot,
                                      projects[0],
                                      _environment.TempPath);
            }


            if (tryWebSiteProject)
            {
                // Website projects need a solution to build so look for one in the repository path
                // that has this website in it.
                var solutionsWithWebsites = (from solution in VsHelper.GetSolutions(repositoryRoot)
                                             select new
                                             {
                                                 Solution = solution,
                                                 MatchingWebsites = (from p in solution.Projects
                                                                     where p.IsWebSite && NormalizePath(p.AbsolutePath).Equals(NormalizePath(targetPath))
                                                                     select p).ToList()
                                             }
                                             into websitePair
                                             where websitePair.MatchingWebsites.Count == 1
                                             select websitePair).ToList();

                // More than one solution is ambiguous
                if (solutionsWithWebsites.Count > 1)
                {
                    throw new InvalidOperationException("Unable to determine which solution file to build.");
                }
                else if (solutionsWithWebsites.Count == 1)
                {
                    // Unambiguously pick the root
                    return new WebSiteBuilder(_propertyProvider, 
                                              repositoryRoot, 
                                              solutionsWithWebsites[0].Solution.Path, 
                                              targetPath);
                }

            }

            // If there's none then use the basic builder (the site is xcopy deployable)
            return new BasicBuilder(repositoryRoot, _environment.TempPath);
        }

        private string NormalizePath(string path)
        {
            return path.ToUpperInvariant().TrimEnd('\\');
        }
    }
}
