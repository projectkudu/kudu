namespace Kudu.Core.SourceControl
{
    public class LineDiff
    {
        public LineDiff(ChangeType type, string text)
        {
            Type = type;
            Text = text;
        }
        public ChangeType Type { get; set; }
        public string Text { get; set; }
        public int? LeftLine { get; set; }
        public int? RightLine { get; set; }
    }
}
