using System.IO;
using Kudu.Core.Infrastructure;
using Kudu.Contracts;

namespace Kudu.Core.SourceControl.Git
{
    public class GitExeServer : IGitServer
    {
        private readonly Executable _gitExe;
        private readonly IProfiler _profiler;

        public GitExeServer(string path, IProfiler profiler)
            : this(GitUtility.ResolveGitPath(), path, profiler)
        {
        }

        public GitExeServer(string pathToGitExe, string path, IProfiler profiler)
        {
            _gitExe = new Executable(pathToGitExe, path);
            _profiler = profiler;
        }

        public void AdvertiseReceivePack(Stream output)
        {
            using (_profiler.Step("GitExeServer.AdvertiseReceivePack"))
            {
                Advertise("receive-pack", output);
            }
        }

        public void AdvertiseUploadPack(Stream output)
        {
            using (_profiler.Step("GitExeServer.AdvertiseUploadPack"))
            {
                Advertise("upload-pack", output);
            }
        }

        public void Receive(Stream inputStream, Stream outputStream)
        {
            using (_profiler.Step("GitExeServer.Receive"))
            {
                ServiceRpc("receive-pack", inputStream, outputStream);
            }
        }

        public void Upload(Stream inputStream, Stream outputStream)
        {
            using (_profiler.Step("GitExeServer.Upload"))
            {
                ServiceRpc("upload-pack", inputStream, outputStream);
            }
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
