namespace Kudu.Core.Infrastructure {
    internal static class ParserHelpers {
        internal static void ParseSummaryFooter(string line, ChangeSetDetail detail) {
            // n files changed, n insertions(+), n deletions(-)
            var subReader = line.AsReader();
            subReader.SkipWhitespace();
            detail.FilesChanged = subReader.ReadInt();
            subReader.ReadUntil(',');
            subReader.Skip(1);
            subReader.SkipWhitespace();
            detail.Insertions = subReader.ReadInt();
            subReader.ReadUntil(',');
            subReader.Skip(1);
            subReader.SkipWhitespace();
            detail.Deletions = subReader.ReadInt();
        }

        internal static bool IsSingleLineFeed(string value) {
            return value.Length == 1 && value[0] == '\n';
        }
    }
}
