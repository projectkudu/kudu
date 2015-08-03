using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public class StructuredTextDocumentEntry<T>
    {
        public T LogEntry { get; set; }
        public IEnumerable<StructuredTextDocumentEntry<T>> Children { get; set; }
    }
}
