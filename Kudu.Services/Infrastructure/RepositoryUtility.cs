using System;
using Kudu.Core.SourceControl;

namespace Kudu.Services.Infrastructure
{
    public static class RepositoryUtility
    {
        public static void EnsureRepository(IRepositoryManager repositoryManager,
                                            RepositoryType expectedType)
        {
            RepositoryType currentType = repositoryManager.GetRepositoryType();

            if (currentType == RepositoryType.None)
            {
                repositoryManager.CreateRepository(expectedType);
            }
            else if (currentType != expectedType)
            {
                throw new InvalidOperationException("Unexpected repository type: " + currentType);
            }
        }
    }
}
