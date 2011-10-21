using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Client.Infrastructure;
using Kudu.Client.Model;
using Kudu.Core.Commands;
using Kudu.Core.Editor;
using SignalR.Hubs;
using Kudu.Core.SourceControl;

namespace Kudu.Client.Hubs {
    public class DevelopmentEnvironment : Hub {
        private readonly ISiteConfiguration _configuration;
        private readonly IUserInformation _userInformation;
        private static readonly string[] _supportedExtensions = new[] { ".csproj", ".vbproj" };
        private const string WapGuid = "349C5851-65DF-11DA-9384-00065B846F21";

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

        private IEditorFileSystem FileSystem {
            get {
                if (CurrentMode == Mode.Development) {
                    return _configuration.DevFileSystem;
                }
                return _configuration.FileSystem;
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

        public Project GetProject() {
            var files = FileSystem.GetFiles().ToList();
            var projects = (from path in files
                            where _supportedExtensions.Contains(Path.GetExtension(path),
                                                                StringComparer.OrdinalIgnoreCase)
                            select path).ToList();

            // TODO: Make the information from the file system richer, so we don't have to make so many requests
            projects.RemoveAll(path => {
                string content = FileSystem.ReadAllText(path).ToUpper();
                return !content.Contains(WapGuid);
            });

            return new Project {
                Name = Caller.applicationName,
                Projects = projects,
                Files = from path in files
                        select new ProjectFile {
                            Path = path
                        }
            };
        }

        public string OpenFile(string path) {
            return FileSystem.ReadAllText(path);
        }

        public void SaveAllFiles(IEnumerable<ProjectFile> files) {
            IEditorFileSystem fileSystem = FileSystem;
            foreach (var file in files) {
                fileSystem.WriteAllText(file.Path, file.Content);
            }
        }

        public void SaveFile(ProjectFile file) {
            FileSystem.WriteAllText(file.Path, file.Content);
        }

        public void DeleteFile(string path) {
            FileSystem.Delete(path);
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
            // TODO: Pass in the solution file
            CommandExecutor.ExecuteCommand(@"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe");
        }

        private enum Mode {
            Live,
            Development
        }
    }
}
