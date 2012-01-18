using System;
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

        public ISiteBuilder CreateBuilder()
        {
            // Get all solutions in the current repository path
            var solutions = VsSolution.GetSolutions(_environment.DeploymentRepositoryPath).ToList();

            if (!solutions.Any())
            {
                // If there's none then use the basic builder (the site is xcopy deployable)
                return new BasicBuilder(_environment.DeploymentRepositoryPath);
            }

            // More than one solution is ambiguous
            if (solutions.Count > 1)
            {
                throw new InvalidOperationException("Unable to determine which solution file to build.");
            }

            // We have a solution
            VsSolution solution = solutions[0];

            // TODO: We need to determine what project to deploy so get a list of all web projects and
            // figure out with some heuristic, which one to deploy.
            // For now just pick the first one we find.
            VsSolutionProject project = solution.Projects.Where(p => p.IsWap || p.IsWebSite).FirstOrDefault();

            if (project == null)
            {
                throw new InvalidOperationException("Unable to find a target project to build. No web projects found.");
            }

            if (project.IsWap)
            {
                return new WapBuilder(_propertyProvider, _environment.DeploymentRepositoryPath, solution.Path, project.AbsolutePath, _environment.TempPath);
            }

            return new WebSiteBuilder(_propertyProvider, _environment.DeploymentRepositoryPath, solution.Path, project.AbsolutePath);
        }
    }
}
