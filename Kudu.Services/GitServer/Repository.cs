// This file was modified from the one found in git-dot-aspx

namespace Kudu.Services.GitServer {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using GitSharp.Core;
    using GitSharp.Core.Transport;

    public class Repository {
        private DirectoryInfo directory;

        public static Repository Open(DirectoryInfo directory) {
            if (GitSharp.Repository.IsValid(directory.FullName)) {
                return new Repository(directory);
            }

            return null;
        }

        public Repository(string directory)
            : this(new DirectoryInfo(directory)) {
        }

        public Repository(DirectoryInfo directory) {
            this.directory = directory;
        }

        public void AdvertiseUploadPack(Stream output) {
            using (var repository = GetRepository()) {
                var pack = new UploadPack(repository);
                pack.sendAdvertisedRefs(new RefAdvertiser.PacketLineOutRefAdvertiser(new PacketLineOut(output)));
            }
        }

        public void AdvertiseReceivePack(Stream output) {
            using (var repository = GetRepository()) {
                var pack = new ReceivePack(repository);
                pack.SendAdvertisedRefs(new RefAdvertiser.PacketLineOutRefAdvertiser(new PacketLineOut(output)));
            }
        }

        public void Receive(Stream inputStream, Stream outputStream) {
            using (var repository = GetRepository()) {
                var pack = new ReceivePack(repository);
                pack.setBiDirectionalPipe(false);
                pack.receive(inputStream, outputStream, outputStream);
            }
        }

        public void Upload(Stream inputStream, Stream outputStream) {
            using (var repository = GetRepository()) {
                using (var pack = new UploadPack(repository)) {
                    pack.setBiDirectionalPipe(false);
                    pack.Upload(inputStream, outputStream, outputStream);
                }
            }
        }

        public CommitInfo GetLatestCommit() {
            using (var repository = new GitSharp.Repository(FullPath)) {
                var commit = repository.Head.CurrentCommit;

                if (commit == null) {
                    return null;
                }

                return new CommitInfo {
                    Message = commit.Message,
                    Date = commit.CommitDate.DateTime
                };
            }
        }

        private GitSharp.Core.Repository GetRepository() {
            return GitSharp.Core.Repository.Open(directory);
        }

        public string Name {
            get { return directory.Name; }
        }

        public string FullPath {
            get { return directory.FullName; }
        }

        public string GitDirectory() {
            if (FullPath.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) {
                return FullPath;
            }

            return Path.Combine(FullPath, ".git");
        }

        public void UpdateServerInfo() {
            using (var rep = GetRepository()) {
                if (rep.ObjectDatabase is ObjectDirectory) {
                    RefWriter rw = new SimpleRefWriter(rep, rep.getAllRefs().Values);
                    rw.writePackedRefs();
                    rw.writeInfoRefs();

                    var packs = GetPackRefs(rep);
                    WriteInfoPacks(packs, rep);
                }
            }
        }

        private void WriteInfoPacks(IEnumerable<string> packs, GitSharp.Core.Repository repository) {

            var w = new StringBuilder();

            foreach (string pack in packs) {
                w.Append("P ");
                w.Append(pack);
                w.Append('\n');
            }

            var infoPacksPath = Path.Combine(repository.ObjectsDirectory.FullName, "info/packs");
            var encoded = Encoding.ASCII.GetBytes(w.ToString());


            using (Stream fs = File.Create(infoPacksPath)) {
                fs.Write(encoded, 0, encoded.Length);
            }
        }

        private IEnumerable<string> GetPackRefs(GitSharp.Core.Repository repository) {
            var packDir = repository.ObjectsDirectory.GetDirectories().SingleOrDefault(x => x.Name == "pack");

            if (packDir == null) {
                return Enumerable.Empty<string>();
            }

            return packDir.GetFiles("*.pack").Select(x => x.Name).ToList();
        }
    }

    public class CommitInfo {
        public string Message { get; set; }
        public DateTime Date { get; set; }
    }
}
