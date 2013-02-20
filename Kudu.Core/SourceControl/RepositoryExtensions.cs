using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.SourceControl
{
    public static class RepositoryExtensions
    {
        public static bool IsEmpty(this IRepository repository)
        {
            try
            {
                return string.IsNullOrEmpty(repository.CurrentId);
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
