using System;
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

        public SiteConfiguration(IApplication application, ICredentialProvider credentialProvider)
        {
            ServiceUrl = application.ServiceUrl;
            SiteUrl = application.SiteUrl;
            Name = application.Name;

            // This is to work around a SignalR bug where the hub is still created
            // even if IDisconnect isn't implemented
            if (String.IsNullOrEmpty(ServiceUrl))
            {
                return;
            }

            SiteConfiguration config;
            if (_cache.TryGetValue(ServiceUrl, out config))
            {
                Repository = config.Repository;
                ProjectSystem = config.ProjectSystem;
                DevProjectSystem = config.DevProjectSystem;
                DeploymentManager = config.DeploymentManager;
                CommandExecutor = config.CommandExecutor;
                DevCommandExecutor = config.DevCommandExecutor;
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

                _cache[ServiceUrl] = this;
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
            CommandExecutor.CommandEvent += OnCommandEvent;

            var devCommandExecutor = new RemoteCommandExecutor(ServiceUrl + "dev/command");
            devCommandExecutor.Credentials = credentialProvider.GetCredentials();
            DevCommandExecutor = devCommandExecutor;
            DevCommandExecutor.CommandEvent += OnCommandEvent;

            //try
            //{
            //    // Start the connections
            //    deploymentManager.Start();
            //    commandExecutor.Start();
            //    devCommandExecutor.Start();
            //}
            //catch(Exception ex)
            //{
            //    Debug.WriteLine("Failed to subcribe for updates => " + ex.Message);
            //}
        }

        private void OnDeploymentStatusChanged(DeployResult result)
        {
            var clients = Hub.GetClients<Kudu.SignalR.Hubs.Deployment>();
            clients[Name].updateDeployStatus(new DeployResultViewModel(result));
        }

        private void OnCommandEvent(CommandEvent commandEvent)
        {
            dynamic clients = Hub.GetClients<DevelopmentEnvironment>();
            if (commandEvent.EventType == CommandEventType.Complete)
            {
                clients[Name].commandComplete();
            }
            else
            {
                clients[Name].processCommand(commandEvent.Data);
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

        public RemoteDeploymentManager DeploymentManager
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