namespace Kudu.Core.Deployment
{
    public interface ISiteBuilderFactory
    {
        ISiteBuilder CreateBuilder(ILogger logger);
    }
}
