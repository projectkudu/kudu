using System.IO;

namespace Kudu.Core.SourceControl.Git
{
    public interface IGitServer : IServerRepository
    {
        void SetAuthor(string author);
        void AdvertiseUploadPack(Stream output);
        void AdvertiseReceivePack(Stream output);
        bool Receive(Stream inputStream, Stream outputStream);
        void Upload(Stream inputStream, Stream outputStream);
    }
}
