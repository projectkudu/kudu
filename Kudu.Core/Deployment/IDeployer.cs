using System;
namespace Kudu.Core.Deployment {
    public interface IDeployer {
        void Deploy(string id);
    }
}
