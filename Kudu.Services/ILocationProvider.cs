using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kudu.Services {
    public interface ILocationProvider {
        string RepositoryRoot { get; }
    }
}
