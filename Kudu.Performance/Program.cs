using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using Kudu.TestHarness;

namespace Kudu.Performance
{
    class Program
    {
        static void Main(string[] args)
        {
            Debug.Listeners.Add(new ConsoleTraceListener());

            RunProfile("https://github.com/davidebbo/Express-Template.git");
        }

        private static void RunProfile(string cloneUrl)
        {
            var name = cloneUrl.Split('/').Last().Replace(".git", "");
            RunProfile(name, cloneUrl);
        }

        private static void RunProfile(string applicationName, string cloneUrl)
        {
            string serverProfile = null;
            Stopwatch sw = null;
            long fileCount;
            long gitDirectorySizeBytes;
            long directorySizeBytes;

            Log("Cloning from {0}", cloneUrl);
            using (var repo = Git.Clone(applicationName, cloneUrl))
            {
                Log("Creating site {0}", applicationName);
                ApplicationManager.Run(applicationName, appManager =>
                {
                    Log("Starting git deploy");
                    sw = Stopwatch.StartNew();
                    appManager.GitDeploy(applicationName);
                    sw.Stop();
                    Log("Complete");

                    Log("Collecting server profile...");
                    var client = new HttpClient();
                    serverProfile = client.GetAsync(appManager.ServiceUrl + "profile").Result.Content.ReadAsStringAsync().Result;
                });

                Log("Collecting repository stats");
                fileCount = Directory.GetFiles(repo.PhysicalPath, "*.*", SearchOption.AllDirectories).LongLength;
                directorySizeBytes = GetDirectorySize(repo.PhysicalPath);
                gitDirectorySizeBytes = GetDirectorySize(Path.Combine(repo.PhysicalPath, ".git"));
            }

            Console.WriteLine();
            Console.WriteLine("===============Repository===============");
            Console.WriteLine("{0} files", fileCount);
            Console.WriteLine(".git folder size: {0}", FormatBytes(gitDirectorySizeBytes));
            Console.WriteLine("folder size: {0}", FormatBytes(directorySizeBytes));
            Console.WriteLine();

            Console.WriteLine("===============Deployment===============");
            Console.WriteLine("Time to push : {0:0.000}s", sw.Elapsed.TotalSeconds);
            var profilePath = Path.Combine("profiles", applicationName + ".profile.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
            File.WriteAllText(profilePath, serverProfile);
            Console.WriteLine("Server profile for is saved to {0}", Path.GetFullPath(profilePath));
            Console.WriteLine();
        }

        private static void Log(string value, params object[] args)
        {
            Console.WriteLine("[" + DateTime.Now.ToShortTimeString() + "]: " + value, args);
        }

        private static long GetDirectorySize(string path)
        {
            var directoryInfo = new DirectoryInfo(path);
            return directoryInfo.GetFiles("*.*", SearchOption.AllDirectories).Sum(file => file.Length);
        }

        public static string FormatBytes(long bytes)
        {
            const int scale = 1024;
            string[] orders = new string[] { "GB", "MB", "KB", "Bytes" };
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if (bytes > max)
                {
                    return String.Format("{0:##.##} {1}", Decimal.Divide(bytes, max), order);
                }

                max /= scale;
            }
            return "0 Bytes";
        }
    }
}
