using System;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.SourceControl.Git
{
    internal class GitExecutable : Executable
    {
        public GitExecutable(string workingDirectory, TimeSpan idleTimeout)
            : base(PathUtilityFactory.Instance.ResolveGitPath(), workingDirectory, idleTimeout)
        {
        }

        public void SetTraceLevel(int level)
        {
            EnvironmentVariables[KnownVariables.GIT_TRACE] = level.ToString();
        }

        public void SetHttpVerbose(bool verbose)
        {
            if (verbose)
            {
                EnvironmentVariables[KnownVariables.GIT_CURL_VERBOSE] = "1";
            }
            else
            {
                EnvironmentVariables.Remove(KnownVariables.GIT_CURL_VERBOSE);
            }
        }

        public void SetSSLNoVerify(bool verify)
        {
            EnvironmentVariables[KnownVariables.GIT_SSL_NO_VERIFY] = verify.ToString().ToLowerInvariant();
        }


        private static class KnownVariables
        {
            public const string GIT_CURL_VERBOSE = "GIT_CURL_VERBOSE";
            public const string GIT_TRACE = "GIT_TRACE";
            public const string GIT_SSL_NO_VERIFY = "GIT_SSL_NO_VERIFY";
        }
    }
}
