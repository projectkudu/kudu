namespace Kudu.Core.SourceControl
{
    public class PushInfo
    {
        public string OldId { get; set; }
        public string NewId { get; set; }
        public Branch Branch { get; set; }
    }
}
