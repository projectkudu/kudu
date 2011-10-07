using System.Collections.Concurrent;
using Kudu.Client.Hubs.Editor;
using Kudu.Client.Model;
using Kudu.Client.Models;
using Kudu.Core.Commands;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using SignalR.Hubs;

namespace Kudu.Client.Infrastructure {
    public class SiteConfiguration : ISiteConfiguration {
        private static readonly ConcurrentDictionary<string, SiteConfiguration> _cache = new ConcurrentDictionary<string, SiteConfiguration>();

        public SiteConfiguration(IApplication application) {
            ServiceUrl = application.ServiceUrl;
            SiteUrl = application.SiteUrl;
            Name = application.Name;

            SiteConfiguration config;
            if (_cache.TryGetValue(Name, out config)) {
                Repository = config.Repository;
                FileSystem = config.FileSystem;
                RepositoryManager = config.RepositoryManager;
                DevFileSystem = config.DevFileSystem;


                if (config.DeploymentManager.IsActive) {
                    DeploymentManager = config.DeploymentManager;
                    CommandExecutor = config.CommandExecutor;
                    DevCommandExecutor = config.DevCommandExecutor;
                }
                else {
                    DeploymentManager = new RemoteDeploymentManager(ServiceUrl + "deploy");
                    DeploymentManager.StatusChanged += OnDeploymentStatusChanged;

                    CommandExecutor = new RemoteCommandExecutor(ServiceUrl + "live/command");
                    DevCommandExecutor = new RemoteCommandExecutor(ServiceUrl + "dev/command");
                    DevCommandExecutor.CommandEvent += commandEvent => {
                        OnCommandEvent<CommandLine>(commandEvent);
                    };

                    CommandExecutor.CommandEvent += commandEvent => {
                        OnCommandEvent<CommandLine>(commandEvent);
                    };
                }
            }
            else {
                Repository = new RemoteRepository(ServiceUrl + "scm");
                DeploymentManager = new RemoteDeploymentManager(ServiceUrl + "deploy");
                RepositoryManager = new RemoteRepositoryManager(ServiceUrl + "scm");

                FileSystem = new RemoteFileSystem(ServiceUrl + "live/files");
                CommandExecutor = new RemoteCommandExecutor(ServiceUrl + "live/command");

                DevFileSystem = new RemoteFileSystem(ServiceUrl + "dev/files");
                DevCommandExecutor = new RemoteCommandExecutor(ServiceUrl + "dev/command");

                DevCommandExecutor.CommandEvent += commandEvent => {
                    OnCommandEvent<CommandLine>(commandEvent);
                };
                
                CommandExecutor.CommandEvent += commandEvent => {
                    OnCommandEvent<CommandLine>(commandEvent);
                };

                DeploymentManager.StatusChanged += OnDeploymentStatusChanged;

                _cache[Name] = this;
            }
        }

        private void OnDeploymentStatusChanged(DeployResult result) {
            var clients = Hub.GetClients<Deployment>();
            clients.updateDeployStatus(new DeployResultViewModel(result));
        }

        private void OnCommandEvent<T>(CommandEvent commandEvent) where T : Hub {
            var clients = Hub.GetClients<T>();
            if (commandEvent.EventType == CommandEventType.Complete) {
                clients.done();
            }
            else {
                clients.onData(commandEvent.Data);
            }
        }

        public string Name { get; private set; }
        public string ServiceUrl { get; private set; }
        public string SiteUrl { get; private set; }

        public IEditorFileSystem DevFileSystem {
            get;
            private set;
        }

        public IEditorFileSystem FileSystem {
            get;
            private set;
        }

        public IRepositoryManager RepositoryManager {
            get;
            private set;
        }

        public IRepository Repository {
            get;
            private set;
        }

        IDeploymentManager ISiteConfiguration.DeploymentManager {
            get {
                return DeploymentManager;
            }
        }

        private RemoteDeploymentManager DeploymentManager {
            get;
            set;
        }

        public ICommandExecutor CommandExecutor {
            get;
            private set;
        }

        public ICommandExecutor DevCommandExecutor {
            get;
            private set;
        }
    }
}