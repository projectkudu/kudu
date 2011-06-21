using System;
using System.Linq;
using System.IO;
using Kudu.Core;
using Kudu.Core.Git;

namespace Kudu {
    class Program {
        static void Main(string[] args) {            
            RunInteractive(args);
        }

        private static void RunInteractive(string[] args) {
            string path;
            if (args.Length > 0) {
                path = args[0];
            }
            else {
                path = Directory.CreateDirectory("gitrepo").FullName;
            }

            Console.WriteLine("1. LibGit2 impl");
            Console.WriteLine("2. GitExe impl");
            int ch = Int32.Parse(Console.ReadLine());

            IRepository repo = ch == 1 ? (IRepository)new LibGitRepository(path) : (IRepository)new GitExeRepository(path);
            Console.WriteLine();
            Console.WriteLine("Welcome");
            Console.WriteLine();
            Console.Write("command: ");
            string command;
            while ((command = Console.ReadLine()) != null) {
                if (command.Trim() == "q") {
                    break;
                }

                Process(repo, command);
                Console.Write("command: ");
            }
        }

        private static void Process(IRepository repository, string command) {
            if (command.StartsWith("s", StringComparison.OrdinalIgnoreCase)) {
                RunAction("status", () => {
                    foreach (var s in repository.GetStatus()) {
                        Console.WriteLine("{0} is {1}", s.Path, s.Status);
                    }
                });

            }
            else if (command.StartsWith("a ", StringComparison.OrdinalIgnoreCase)) {
                string path = command.Substring(1).Trim();
                if (String.IsNullOrWhiteSpace(path)) {
                    Console.WriteLine("No file to add!");
                }
                else {
                    RunAction("add", () => repository.AddFile(path));
                }
            }
            else if (command.StartsWith("r ", StringComparison.OrdinalIgnoreCase)) {
                string path = command.Substring(1).Trim();
                if (String.IsNullOrWhiteSpace(path)) {
                    Console.WriteLine("No file to remove!");
                }
                else {
                    RunAction("remove", () => repository.RemoveFile(path));
                }
            }
            else if (command.StartsWith("l", StringComparison.OrdinalIgnoreCase)) {
                RunAction("log", () => {
                    foreach (var c in repository.GetChanges()) {
                        Console.WriteLine(c);
                    }
                });
            }
            else if (command.StartsWith("i", StringComparison.OrdinalIgnoreCase)) {
                RunAction("init", repository.Initialize);
            }
            else if (command.StartsWith("c ", StringComparison.OrdinalIgnoreCase)) {
                string[] parts = command.Substring(1).Trim().Split(' ');
                if (parts.Length < 2) {
                    Console.WriteLine("Name and message required");
                }
                else {
                    RunAction("commit", () => {
                        var commit = repository.Commit(parts[0], parts[1]);
                        Console.WriteLine(commit);
                    });
                }
            }
        }

        private static void RunAction(string actionName, Action action) {
            try {
                action();
            }
            catch (NotImplementedException) {
                Console.WriteLine(actionName + " is not supported");
            }
            catch (Exception e) {
                Console.WriteLine("Someting went wrong: {0}", e);
            }
        }
    }
}
