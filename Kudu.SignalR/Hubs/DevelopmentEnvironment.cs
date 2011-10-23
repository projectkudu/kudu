using System.Collections.Generic;
using Kudu.Core.Commands;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Kudu.SignalR.Infrastructure;
using Kudu.SignalR.ViewModels;
using SignalR.Hubs;

namespace Kudu.SignalR.Hubs {
    public class DevelopmentEnvironment : Hub {
        private readonly ISiteConfiguration _configuration;
        private readonly IUserInformation _userInformation;

        public DevelopmentEnvironment(IUserInformation userInformation,
                                      ISiteConfiguration configuration) {
            _userInformation = userInformation;
            _configuration = configuration;
        }

        private IRepository Repository {
            get {
                return _configuration.Repository;
            }
        }

        private IProjectSystem ProjectSystem {
            get {
                if (CurrentMode == Mode.Development) {
                    return _configuration.DevProjectSystem;
                }
                return _configuration.ProjectSystem;
            }
        }

        private ICommandExecutor CommandExecutor {
            get {
                if (CurrentMode == Mode.Development) {
                    return _configuration.DevCommandExecutor;
                }
                return _configuration.CommandExecutor;
            }
        }

        private Mode CurrentMode {
            get {
                var mode = (Mode?)Caller.mode;
                if (mode == null) {
                    return Mode.Live;
                }
                return mode.Value;
            }
        }

        public ProjectViewModel GetProject() {
            string name = Caller.applicationName;
            var projectViewModel = new ProjectViewModel(name, ProjectSystem.GetProject());
            Caller.defaultSolution = projectViewModel.DefaultSolution;
            return projectViewModel;
        }

        public string OpenFile(string path) {
            return ProjectSystem.ReadAllText(path);
        }

        public void SaveAllFiles(IEnumerable<ProjectFile> files) {
            IProjectSystem fileSystem = ProjectSystem;
            foreach (var file in files) {
                fileSystem.WriteAllText(file.Path, file.Content);
            }
        }

        public void SaveFile(ProjectFile file) {
            ProjectSystem.WriteAllText(file.Path, file.Content);
        }

        public void DeleteFile(string path) {
            ProjectSystem.Delete(path);
        }

        public ChangeSetDetailViewModel GetWorking() {
            ChangeSetDetail workingChanges = Repository.GetWorkingChanges();
            if (workingChanges != null) {
                return new ChangeSetDetailViewModel(workingChanges);
            }
            return null;
        }

        public ChangeSetViewModel Commit(string message) {
            var changeSet = Repository.Commit(_userInformation.UserName, message);
            if (changeSet != null) {
                return new ChangeSetViewModel(changeSet);
            }
            return null;
        }

        public void RevertFile(string path) {
            Repository.RevertFile(path);
        }

        public void GoLive() {
            // Push then deploy

            // TODO: If there's nothing being pushed then don't bother deploying
            _configuration.Repository.Push();
            _configuration.DeploymentManager.Deploy();
        }

        public void ExecuteCommand(string command) {
            CommandExecutor.ExecuteCommand(command);
        }

        public void Build() {
            string solutionFile = Caller.defaultSolution;
            CommandExecutor.ExecuteCommand(@"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe " + solutionFile);
        }

        private enum Mode {
            Live,
            Development
        }
    }
}
