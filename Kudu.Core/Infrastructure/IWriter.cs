using System;

namespace Kudu.Core.Infrastructure
{
    internal interface IWriter
    {
        event Action BeforeWrite;
    }
}
