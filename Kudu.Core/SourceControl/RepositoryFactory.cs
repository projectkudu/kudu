using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl
{
    // TODO: Add unit tests via FileSystem once they add support for EnumerateFiles
    public class RepositoryFactory
    {
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;
        private readonly RepositoryConfiguration _repoConfig;
        private readonly IDeploymentSettingsManager _settings;

        public RepositoryFactory(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, RepositoryConfiguration repositoryConfiguration)
        {
            _environment = environment;
            _settings = settings;
            _traceFactory = traceFactory;
            _repoConfig = repositoryConfiguration;
        }

        /// <summary>
        /// Hieruistically guesses if there's a Mercurial repository at the repositoryPath
        /// </summary>
        private bool IsHgRepository
        {
            get
            {
                string hgRepoFiles = Path.Combine(_environment.RepositoryPath, ".hg");
                return Directory.Exists(hgRepoFiles) && 
                       Directory.EnumerateFiles(hgRepoFiles).Any();
            }
        }

        private bool IsGitRepository
        {
            get
            {
                string gitRepoFiles = Path.Combine(_environment.RepositoryPath, ".git");
                return Directory.Exists(gitRepoFiles) &&
                       Directory.GetFiles(gitRepoFiles).Length > 0;
            }
        }

        public IRepository EnsureRepository(RepositoryType repositoryType)
        {
            if (repositoryType == RepositoryType.Mercurial)
            {
                if (IsGitRepository)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_MismatchRepository, repositoryType, RepositoryType.Git, _environment.RepositoryPath));
                }
                FileSystemHelpers.EnsureDirectory(_environment.RepositoryPath);
                var hgRepository = new HgRepository(_environment.RepositoryPath, _environment.SiteRootPath, _settings, _traceFactory);
                if (!hgRepository.Exists)
                {
                    hgRepository.Initialize(_repoConfig);
                }
                return hgRepository;
            }
            else 
            {
                if (IsHgRepository)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_MismatchRepository, repositoryType, RepositoryType.Mercurial, _environment.RepositoryPath));
                }
                var gitRepository = new GitExeRepository(_environment.RepositoryPath, _environment.SiteRootPath, _settings, _traceFactory);
                if (!gitRepository.Exists)
                {
                    gitRepository.Initialize(_repoConfig);
                }
                return gitRepository;
            }
        }

        public IRepository GetRepository()
        {
            string hgRepoFiles = Path.Combine(_environment.RepositoryPath, ".hg");
            ITracer tracer = _traceFactory.GetTracer();
            if (IsHgRepository)
            {
                tracer.Trace("Found mercurial repository at {0}", _environment.RepositoryPath);
                return new HgRepository(_environment.RepositoryPath, _environment.SiteRootPath, _settings, _traceFactory);
            }
            else if (IsGitRepository)
            {
                tracer.Trace("Assuming git repository at {0}", _environment.RepositoryPath);
                return new GitExeRepository(_environment.RepositoryPath, _environment.SiteRootPath, _settings, _traceFactory);
            }
            return null;
        }
    }
}
