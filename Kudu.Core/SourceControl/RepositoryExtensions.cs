using System;

namespace Kudu.Core.SourceControl
{
    public static class RepositoryExtensions
    {
        public static bool IsEmpty(this IRepository repository)
        {
            try
            {
                return String.IsNullOrEmpty(repository.CurrentId);
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
