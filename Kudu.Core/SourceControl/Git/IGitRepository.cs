using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.SourceControl.Git
{
    interface IGitRepository : IRepository
    {
        bool SkipPostReceiveHookCheck { get; set; }
    }
}
