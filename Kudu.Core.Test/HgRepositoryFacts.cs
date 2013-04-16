using System;
using System.Collections.Generic;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Mercurial;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Test
{
    public class HgRepositoryFacts
    {
        [Fact]
        public void ConvertThrowsIfFileStateIsInvalid()
        {
            // Arrange
            FileState fileState = (FileState)12;

            // Act and Assert
            var ex = Assert.Throws<InvalidOperationException>(() => HgRepository.Convert(fileState));
            Assert.Equal("Unsupported status '12'.", ex.Message);
        }

        [Theory]
        [InlineData(FileState.Clean)]
        [InlineData(FileState.Ignored)]
        [InlineData(FileState.Missing)]
        public void ConvertThrowsIfFileStateIsAndInvalidDiffState(FileState fileState)
        {
            // Act and Assert
            var ex = Assert.Throws<InvalidOperationException>(() => HgRepository.Convert(fileState));
            Assert.Equal(String.Format("Unsupported status '{0}'.", fileState.ToString()), ex.Message);
        }

        [Theory]
        [InlineData(FileState.Added, ChangeType.Added)]
        [InlineData(FileState.Modified, ChangeType.Modified)]
        [InlineData(FileState.Removed, ChangeType.Deleted)]
        [InlineData(FileState.Unknown, ChangeType.Untracked)]
        public void ConvertMapsToChangeTypeForKnonwStates(FileState fileState, ChangeType expected)
        {
            // Act
            ChangeType actual = HgRepository.Convert(fileState);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ParseSummaryReadsSummaryLineFromDiff()
        {
            // Arrange
            string input = @"Bar.txt   |  1 -
 Baz.txt   |  1 +
 Hello.txt |  2 +-
 3 files changed, 2 insertions(+), 2 deletions(-)";
            var changeSetDetail = new ChangeSetDetail();

            // Act
            HgRepository.ParseSummary(input.AsReader(), changeSetDetail);

            // Assert
            Assert.Equal(3, changeSetDetail.FilesChanged);
            Assert.Equal(2, changeSetDetail.Insertions);
            Assert.Equal(2, changeSetDetail.Deletions);
        }

        [Fact]
        public void FetchWithoutConflictsRetriesWithRecoveryIfInitialFetchFails()
        {
            // Arrange
            var executable = new Mock<IExecutable>();
            executable.Setup(e => e.EnvironmentVariables).Returns(new Dictionary<string, string>());
            executable.Setup(e => e.Execute(It.IsAny<ITracer>(), "pull {0} --branch {1} --noninteractive", It.IsAny<object[]>()))
                      .Throws(new CommandLineException("hg.exe", "", "Fetching\r\nabort: abandoned transaction found - run hg recover!\r\n") { ExitCode = 255 })
                      .Verifiable();
            executable.Setup(e => e.Execute(It.IsAny<ITracer>(), "recover"))
                      .Verifiable();


            var hgRepository = new HgRepository(executable.Object, @"x:\some-path", Mock.Of<ITraceFactory>());

            // Act and Assert
            Assert.Throws<CommandLineException>(() => hgRepository.FetchWithoutConflict("https://some-remote", "default"));
            executable.Verify(e => e.Execute(It.IsAny<ITracer>(), "pull {0} --branch {1} --noninteractive", It.IsAny<object[]>()), Times.Exactly(2));
            executable.Verify(e => e.Execute(It.IsAny<ITracer>(), "recover", It.IsAny<object[]>()), Times.Once());
        }

        [Fact]
        public void FetchWithoutConflictsDoesNotExecuteRecoverIfFirstAttemptSucceeds()
        {
            // Arrange
            var executable = new Mock<IExecutable>();
            executable.Setup(e => e.EnvironmentVariables).Returns(new Dictionary<string, string>());
            executable.Setup(e => e.Execute(It.IsAny<ITracer>(), "pull {0} --branch {1} --noninteractive", It.IsAny<object[]>()))
                      .Returns(Tuple.Create("foo", "bar"))
                      .Verifiable();
            executable.Setup(e => e.Execute(It.IsAny<ITracer>(), "recover"))
                      .Verifiable();


            var hgRepository = new HgRepository(executable.Object, @"x:\some-path", Mock.Of<ITraceFactory>());

            // Act
            hgRepository.FetchWithoutConflict("https://some-remote", "default");

            // Assert
            executable.Verify(e => e.Execute(It.IsAny<ITracer>(), "pull {0} --branch {1} --noninteractive", It.IsAny<object[]>()), Times.Once());
            executable.Verify(e => e.Execute(It.IsAny<ITracer>(), "recover", It.IsAny<object[]>()), Times.Never());
        }

        [Fact]
        public void FetchWithoutConflictOnHgEmptyRepo()
        {
            // Arrange
            var executable = new Mock<IExecutable>();
            executable.Setup(e => e.EnvironmentVariables).Returns(new Dictionary<string, string>());
            executable.Setup(e => e.Execute(It.IsAny<ITracer>(), "pull {0} --branch {1} --noninteractive", It.IsAny<object[]>()))
                      .Callback((ITracer tracer, string arguments, object[] args) => { throw new CommandLineException("hg.exe", "pull", "abort: unknown branch 'default'!"); });

            var hgRepository = new HgRepository(executable.Object, @"x:\some-path", Mock.Of<ITraceFactory>());

            // Act
            Assert.Throws<BranchNotFoundException>(() => hgRepository.FetchWithoutConflict("https://some-remote", "default"));
        }
    }
}
