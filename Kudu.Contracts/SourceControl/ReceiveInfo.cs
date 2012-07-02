namespace Kudu.Core.SourceControl
{
    public class ReceiveInfo
    {
        public string OldId { get; set; }
        public string NewId { get; set; }
        public Branch Branch { get; set; }
    }
}
