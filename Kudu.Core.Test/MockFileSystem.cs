using Kudu.Core.Infrastructure;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.Test
{
    static class MockFileSystem
    {
        public static IFileSystem GetMockFileSystem(string file, Func<string> content)
        {
            var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            var dirBase = new Mock<DirectoryBase>(MockBehavior.Strict);
            var dirInfoBase = new Mock<DirectoryInfoBase>(MockBehavior.Strict);
            var dirInfoFactory = new Mock<IDirectoryInfoFactory>(MockBehavior.Strict);

            // Setup
            fileSystem.Setup(f => f.File)
              .Returns(fileBase.Object);
            fileSystem.Setup(f => f.Directory)
              .Returns(dirBase.Object);
            fileSystem.Setup(f => f.DirectoryInfo)
              .Returns(dirInfoFactory.Object);

            fileBase.Setup(f => f.Exists(It.IsAny<string>()))
                    .Returns((string path) => path == file && content() != null);
            fileBase.Setup(f => f.OpenRead(It.IsAny<string>()))
                    .Returns((string path) =>
                    {
                        if (path == file)
                        {
                            return new MemoryStream(Encoding.UTF8.GetBytes(content()));
                        }

                        throw new InvalidOperationException("Should not reach here!");
                    });
            fileBase.Setup(f => f.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));
            fileBase.Setup(f => f.ReadAllText(It.IsAny<string>()))
                    .Returns((string path) =>
                    {
                        using (var reader = new StreamReader(fileBase.Object.OpenRead(path)))
                        {
                            return reader.ReadToEnd();
                        }
                    });
            fileBase.Setup(f => f.ReadAllLines(It.IsAny<string>()))
                    .Returns((string path) =>
                    {
                        return fileBase.Object.ReadAllText(path).Split(new[] { System.Environment.NewLine }, StringSplitOptions.None);
                    });

            dirInfoFactory.Setup(d => d.FromDirectoryName(It.IsAny<string>()))
                          .Returns(dirInfoBase.Object);

            dirBase.Setup(d => d.Exists(It.IsAny<string>()))
                   .Returns((string path) => path == Path.GetDirectoryName(file));

            dirBase.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                   .Returns((string path, string pattern, SearchOption searchOption) => new[] { file });

            dirInfoBase.SetupGet(d => d.Exists)
                       .Returns(true);
            dirInfoBase.SetupSet(d => d.Attributes = FileAttributes.Normal);
            dirInfoBase.Setup(d => d.GetFileSystemInfos())
                       .Returns(new FileSystemInfoBase[0]);
            dirInfoBase.Setup(d => d.Delete());

            FileSystemHelpers.Instance = fileSystem.Object;

            return fileSystem.Object;
        }
    }
}
