using System.Collections.Generic;
using System.Linq;
using Kudu.Client.Model;
using Kudu.Core.Deployment;
using SignalR.Hubs;
using System;

namespace Kudu.Client.Hubs {
    public class Deployment : Hub {
        private readonly IDeploymentManager _deploymentManager;

        public Deployment(IDeploymentManager deploymentManager) {
            _deploymentManager = deploymentManager;
        }

        public IEnumerable<DeployResultViewModel> GetDeployments() {
            string active = _deploymentManager.ActiveDeploymentId;
            Caller.id = active;
            return _deploymentManager.GetResults().Select(d => new DeployResultViewModel(d) {
                Active = active == d.Id
            });
        }

        public IEnumerable<LogEntryViewModel> GetDeployLog(string id) {
            return from entry in _deploymentManager.GetLogEntries(id)
                   select new LogEntryViewModel(entry);
        }

        public void Deploy(string id) {
            if (String.IsNullOrEmpty(id)) {
                _deploymentManager.Deploy();                
            }
            else {
                _deploymentManager.Deploy(id);
            }
        }
    }
}
