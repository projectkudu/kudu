using System.Collections.Concurrent;
using Kudu.Client.Commands;
using Kudu.Client.Deployment;
using Kudu.Client.Editor;
using Kudu.Client.Infrastructure;
using Kudu.Client.SourceControl;
using Kudu.Core.Commands;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Kudu.SignalR.Hubs;
using Kudu.SignalR.Models;
using Kudu.SignalR.ViewModels;
using SignalR.Hubs;

namespace Kudu.SignalR.Infrastructure
{
    public class SiteConfiguration : ISiteConfiguration
    {
        // TODO: We need to expose methods to clean this cache
        private static readonly ConcurrentDictionary<string, SiteConfiguration> _cache = new ConcurrentDictionary<string, SiteConfiguration>();
        private static dynamic devenvClients = Hub.GetClients<DevelopmentEnvironment>();

        public SiteConfiguration(IApplication application, ICredentialProvider credentialProvider)
        {
            ServiceUrl = application.ServiceUrl;
            SiteUrl = application.SiteUrl;
            Name = application.Name;

            SiteConfiguration config;
            if (_cache.TryGetValue(Name, out config))
            {
                Repository = config.Repository;
                ProjectSystem = config.ProjectSystem;
                DevProjectSystem = config.DevProjectSystem;


                if (config.DeploymentManager.IsActive)
                {
                    DeploymentManager = config.DeploymentManager;
                    CommandExecutor = config.CommandExecutor;
                    DevCommandExecutor = config.DevCommandExecutor;
                }
                else
                {
                    SubscribeToEvents(credentialProvider);
                }
            }
            else
            {
                var repository = new RemoteRepository(ServiceUrl + "scm");
                repository.Credentials = credentialProvider.GetCredentials();
                Repository = repository;

                var projectSystem = new RemoteProjectSystem(ServiceUrl + "live/files");
                projectSystem.Credentials = credentialProvider.GetCredentials();
                ProjectSystem = projectSystem;

                var devProjectSystem = new RemoteProjectSystem(ServiceUrl + "dev/files");
                devProjectSystem.Credentials = credentialProvider.GetCredentials();
                DevProjectSystem = devProjectSystem;

                SubscribeToEvents(credentialProvider);

                _cache[Name] = this;
            }
        }

        private void SubscribeToEvents(ICredentialProvider credentialProvider)
        {
            var deploymentManager = new RemoteDeploymentManager(ServiceUrl + "deploy");
            deploymentManager.Credentials = credentialProvider.GetCredentials();
            DeploymentManager = deploymentManager;
            DeploymentManager.StatusChanged += OnDeploymentStatusChanged;


            var commandExecutor = new RemoteCommandExecutor(ServiceUrl + "live/command");
            commandExecutor.Credentials = credentialProvider.GetCredentials();
            CommandExecutor = commandExecutor;
            CommandExecutor.CommandEvent += commandEvent =>
            {
                OnNewCommandEvent(devenvClients, commandEvent);
            };

            var devCommandExecutor = new RemoteCommandExecutor(ServiceUrl + "dev/command");
            devCommandExecutor.Credentials = credentialProvider.GetCredentials();
            DevCommandExecutor = devCommandExecutor;
            DevCommandExecutor.CommandEvent += commandEvent =>
            {
                OnNewCommandEvent(devenvClients, commandEvent);
            };
        }

        private void OnDeploymentStatusChanged(DeployResult result)
        {
            var clients = Hub.GetClients<Kudu.SignalR.Hubs.Deployment>();
            clients.updateDeployStatus(new DeployResultViewModel(result));
        }

        private void OnNewCommandEvent(dynamic clients, CommandEvent commandEvent)
        {
            if (commandEvent.EventType == CommandEventType.Complete)
            {
                clients.commandComplete();
            }
            else
            {
                clients.processCommand(commandEvent.Data);
            }
        }

        public string Name { get; private set; }
        public string ServiceUrl { get; private set; }
        public string SiteUrl { get; private set; }

        public IProjectSystem DevProjectSystem
        {
            get;
            private set;
        }

        public IProjectSystem ProjectSystem
        {
            get;
            private set;
        }

        public IRepository Repository
        {
            get;
            private set;
        }

        IDeploymentManager ISiteConfiguration.DeploymentManager
        {
            get
            {
                return DeploymentManager;
            }
        }

        private RemoteDeploymentManager DeploymentManager
        {
            get;
            set;
        }

        public ICommandExecutor CommandExecutor
        {
            get;
            private set;
        }

        public ICommandExecutor DevCommandExecutor
        {
            get;
            private set;
        }
    }
}