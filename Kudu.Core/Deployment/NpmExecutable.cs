using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    internal class NpmExecutable : Executable
    {
        public NpmExecutable(string workingDirectory)
            : base(PathUtility.ResolveNpmPath(), workingDirectory)
        {
            Encoding = null;
        }
    }
}
