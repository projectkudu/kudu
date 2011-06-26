namespace Kudu.Core {
    public class Branch {
        public Branch(string id, string name) {
            Id = id;
            Name = name;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
    }
}
