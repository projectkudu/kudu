using System.Threading.Tasks;

namespace Kudu.Core.Deployment {
    public interface ISiteBuilder {
        Task Build(string outputPath, ILogger logger);
    }
}
