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
    public class RepositoryFactory : IRepositoryFactory
    {
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;
        private readonly IDeploymentSettingsManager _settings;

        public RepositoryFactory(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory)
        {
            _environment = environment;
            _settings = settings;
            _traceFactory = traceFactory;
        }

        /// <summary>
        /// Heuristically guesses if there's a Mercurial repository at the repositoryPath
        /// </summary>
        public virtual bool IsHgRepository
        {
            get
            {
                string hgRepoFiles = Path.Combine(_environment.RepositoryPath, ".hg");
                return Directory.Exists(hgRepoFiles) &&
                       Directory.EnumerateFiles(hgRepoFiles).Any();
            }
        }

        public virtual bool IsGitRepository
        {
            get
            {
                var gitRepository = CreateGitRepositoryInstance();
                return gitRepository.Exists;
            }
        }

        public virtual bool NoRepository
        {
            get { return _settings.NoRepository(); }
        }

        public virtual bool IsCustomGitRepository
        {
            get
            {
                var gitRepository = CreateGitRepositoryInstance();
                gitRepository.SkipPostReceiveHookCheck = true;
                return gitRepository.Exists;
            }
        }

        public IRepository EnsureRepository(RepositoryType repositoryType)
        {
            // Validate if conflicting with existing repository
            RepositoryType existingType;
            if (TryGetExistingRepositoryType(out existingType) && existingType != repositoryType)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_MismatchRepository, repositoryType, existingType, _environment.RepositoryPath));
            }

            IRepository repository;
            if (repositoryType == RepositoryType.None)
            {
                repository = new NullRepository(_environment.RepositoryPath, _traceFactory);
            }
            else if (repositoryType == RepositoryType.Mercurial)
            {
                FileSystemHelpers.EnsureDirectory(_environment.RepositoryPath);
                repository = new HgRepository(_environment.RepositoryPath, _environment.RootPath, _settings, _traceFactory);
            }
            else
            {
                repository = CreateGitRepositoryInstance();
            }

            if (!repository.Exists)
            {
                repository.Initialize();
            }
            return repository;
        }

        public IRepository GetRepository()
        {
            ITracer tracer = _traceFactory.GetTracer();
            if (NoRepository)
            {
                tracer.Trace("Assuming none repository at {0}", _environment.RepositoryPath);
                return new NullRepository(_environment.RepositoryPath, _traceFactory);
            }
            else if (IsGitRepository)
            {
                tracer.Trace("Assuming git repository at {0}", _environment.RepositoryPath);
                return CreateGitRepositoryInstance();
            }
            else if (IsHgRepository)
            {
                tracer.Trace("Found mercurial repository at {0}", _environment.RepositoryPath);
                return new HgRepository(_environment.RepositoryPath, _environment.RootPath, _settings, _traceFactory);
            }
            return null;
        }

        public IRepository GetCustomRepository()
        {
            ITracer tracer = _traceFactory.GetTracer();
            if (IsCustomGitRepository)
            {
                tracer.Trace("Assuming custom git repository at {0}", _environment.RepositoryPath);
                var ret = CreateGitRepositoryInstance();
                ret.SkipPostReceiveHookCheck = true;
                return ret;
            }
            return null;
        }

        public IRepository GetGitRepository()
        {
            return CreateGitRepositoryInstance();
        }

        private bool TryGetExistingRepositoryType(out RepositoryType repositoryType)
        {
            if (NoRepository)
            {
                repositoryType = RepositoryType.None;
                return true;
            }
            else if (IsGitRepository)
            {
                repositoryType = RepositoryType.Git;
                return true;
            }
            else if (IsHgRepository)
            {
                repositoryType = RepositoryType.Mercurial;
                return true;
            }

            repositoryType = default(RepositoryType);
            return false;
        }

        private IGitRepository CreateGitRepositoryInstance()
        {
            if (_settings.UseLibGit2SharpRepository())
            {
                return new LibGit2SharpRepository(_environment, _settings, _traceFactory);
            }
            else
            {
                return new GitExeRepository(_environment, _settings, _traceFactory);
            }
        }
    }
}
