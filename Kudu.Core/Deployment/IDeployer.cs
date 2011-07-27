using System.Threading.Tasks;

namespace Kudu.Core.Deployment {
    public interface IDeployer {
        Task Deploy(string targetPath, ILogger logger);
    }
}
