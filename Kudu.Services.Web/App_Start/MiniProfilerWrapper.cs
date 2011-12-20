using System;
using Kudu.Contracts;
using MvcMiniProfiler;

namespace Kudu.Services.Web.App_Start
{
    public class MiniProfilerWrapper : IProfiler
    {
        public IDisposable Step(string value)
        {
            return MiniProfiler.Current.Step(value);
        }
    }
}