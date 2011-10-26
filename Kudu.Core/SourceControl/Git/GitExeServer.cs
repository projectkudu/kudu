using System.IO;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.SourceControl.Git
{
    public class GitExeServer : IGitServer
    {
        private readonly Executable _gitExe;

        public GitExeServer(string path)
            : this(GitUtility.ResolveGitPath(), path)
        {
        }

        public GitExeServer(string pathToGitExe, string path)
        {
            _gitExe = new Executable(pathToGitExe, path);
        }

        public void AdvertiseReceivePack(Stream output)
        {
            Advertise("receive-pack", output);
        }

        public void AdvertiseUploadPack(Stream output)
        {
            Advertise("upload-pack", output);
        }

        public void Receive(Stream inputStream, Stream outputStream)
        {
            ServiceRpc("receive-pack", inputStream, outputStream);
        }

        public void Upload(Stream inputStream, Stream outputStream)
        {
            ServiceRpc("upload-pack", inputStream, outputStream);
        }

        private void Advertise(string serviceName, Stream output)
        {
            _gitExe.Execute(null, output, @"{0} --stateless-rpc --advertise-refs ""{1}""", serviceName, _gitExe.WorkingDirectory);
        }

        private void ServiceRpc(string serviceName, Stream input, Stream output)
        {
            _gitExe.Execute(input, output, @"{0} --stateless-rpc ""{1}""", serviceName, _gitExe.WorkingDirectory);
        }
    }
}
