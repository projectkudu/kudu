using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Kudu.Core.SourceControl.Git {
    public interface IGitServer {
        void AdvertiseUploadPack(Stream output);
        void AdvertiseReceivePack(Stream output);
        void Receive(Stream inputStream, Stream outputStream);
        void Upload(Stream inputStream, Stream outputStream);
    }
}
