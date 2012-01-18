using System.Collections.Generic;
using System.Linq;
using Kudu.Core.Deployment;
using Kudu.SignalR.ViewModels;
using SignalR.Hubs;

namespace Kudu.SignalR.Hubs
{
    public class Deployment : Hub
    {
        private readonly IDeploymentManager _deploymentManager;

        public Deployment(IDeploymentManager deploymentManager)
        {
            _deploymentManager = deploymentManager;
        }

        public void Initialize()
        {
            string applicationName = Caller.applicationName;

            AddToGroup(applicationName);
        }

        public IEnumerable<DeployResultViewModel> GetDeployments()
        {
            // Get the list of deployments
            var deployments = _deploymentManager.GetResults()
                                                .OrderByDescending(d => d.DeployStartTime)
                                                .ToList();

            // Only return the failure if it was the last deployment
            var lastDeployment = deployments.FirstOrDefault();

            if (lastDeployment != null && lastDeployment.Status == DeployStatus.Failed)
            {
                var failed = new DeployResultViewModel(lastDeployment);

                // Return the failed deployment plus the last 5 ones that haven't failed
                return new[] { failed }.Concat(GetTopFive(deployments));
            }

            return GetTopFive(deployments);
        }

        private IEnumerable<DeployResultViewModel> GetTopFive(IEnumerable<DeployResult> results)
        {
            string active = _deploymentManager.ActiveDeploymentId;

            return (from d in results
                    where d.Status != DeployStatus.Failed
                    select new DeployResultViewModel(d)
                    {
                        Current = d.Id == active
                    })
                    .Take(5);
        }

        public IEnumerable<LogEntryViewModel> GetDeployLog(string id)
        {
            return from entry in _deploymentManager.GetLogEntries(id)
                   select new LogEntryViewModel(entry);
        }

        public IEnumerable<LogEntryViewModel> GetDeployLogEntryDetails(string id, string entryId)
        {
            return from entry in _deploymentManager.GetLogEntryDetails(id, entryId)
                   select new LogEntryViewModel(entry);
        }

        public void Remove(string id)
        {
            _deploymentManager.Delete(id);
        }

        public void Deploy(string id)
        {
            _deploymentManager.Deploy(id);
        }

        public void Rebuild(string id)
        {
            _deploymentManager.Deploy(id);
        }
    }
}
