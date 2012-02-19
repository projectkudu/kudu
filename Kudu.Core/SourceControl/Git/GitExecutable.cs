using Kudu.Core.Infrastructure;

namespace Kudu.Core.SourceControl.Git
{
    internal class GitExecutable : Executable
    {
        public GitExecutable(string workingDirectory)
            : base(PathUtility.ResolveGitPath(), workingDirectory)
        {
        }

        public void SetTraceLevel(int level)
        {
            EnvironmentVariables[Environment.GIT_TRACE] = level.ToString();
        }

        public void SetHttpVerbose(bool verbose)
        {
            if (verbose)
            {
                EnvironmentVariables[Environment.GIT_CURL_VERBOSE] = "1";
            }
            else
            {
                EnvironmentVariables.Remove(Environment.GIT_CURL_VERBOSE);
            }
        }


        private class Environment
        {
            public const string GIT_CURL_VERBOSE = "GIT_CURL_VERBOSE";
            public const string GIT_TRACE = "GIT_TRACE";
        }
    }
}
