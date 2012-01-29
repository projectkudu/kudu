using System.Collections.Generic;
using System.Linq;

namespace Kudu.Core.SourceControl
{
    public class NullRepository : IRepository
    {
        private static readonly NullRepository _instance = new NullRepository();

        private NullRepository()
        {
        }

        public static NullRepository Instance
        {
            get
            {
                return _instance;
            }
        }

        public string CurrentId
        {
            get { return null; }
        }

        public void Initialize()
        {

        }

        public IEnumerable<Branch> GetBranches()
        {
            return Enumerable.Empty<Branch>();
        }

        public IEnumerable<FileStatus> GetStatus()
        {
            return Enumerable.Empty<FileStatus>();
        }

        public IEnumerable<ChangeSet> GetChanges()
        {
            return Enumerable.Empty<ChangeSet>();
        }

        public IEnumerable<ChangeSet> GetChanges(int index, int limit)
        {
            return Enumerable.Empty<ChangeSet>();
        }

        public ChangeSetDetail GetDetails(string id)
        {
            return null;
        }

        public ChangeSetDetail GetWorkingChanges()
        {
            return null;
        }

        public void AddFile(string path)
        {

        }

        public void RevertFile(string path)
        {

        }

        public ChangeSet Commit(string authorName, string message)
        {
            return null;
        }

        public void Update(string id)
        {

        }

        public void Push()
        {
        }


        public ChangeSet GetChangeSet(string id)
        {
            return null;
        }
    }
}
