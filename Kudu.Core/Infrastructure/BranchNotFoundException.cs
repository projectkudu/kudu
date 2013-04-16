using System;
using System.Globalization;

namespace Kudu.Core.Infrastructure
{

    public class BranchNotFoundException : InvalidOperationException
    {
        public BranchNotFoundException(string branchName, Exception innerException)
            : base(String.Format(CultureInfo.CurrentCulture, Resources.Error_BranchNotFound, branchName), innerException)
        {
        }
    }
}
