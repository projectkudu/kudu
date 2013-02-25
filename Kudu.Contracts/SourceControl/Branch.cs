namespace Kudu.Core.SourceControl
{
    public abstract class Branch
    {
        protected Branch(string id, string name, bool active)
        {
            Id = id;
            Name = name;
            Active = active;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public bool Active { get; set; }
        public abstract bool IsMaster { get; }
    }
}
