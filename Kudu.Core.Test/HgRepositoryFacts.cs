using System;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Mercurial;
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

    }
}
