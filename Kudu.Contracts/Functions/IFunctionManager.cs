using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Functions
{
    public interface IFunctionManager
    {
        Task SyncTriggers();
    }
}
