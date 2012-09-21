using Kudu.Core.Infrastructure;

namespace Kudu.Core.SSHKey
{
    internal class SSHExecutable : Executable
    {
        public SSHExecutable(string workingDirectory)
            : base(PathUtility.ResolveSSHPath(), workingDirectory)
        {
        }
    }
}
