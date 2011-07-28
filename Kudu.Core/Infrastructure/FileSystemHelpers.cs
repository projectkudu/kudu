using System;
using System.IO;
using System.Threading;

namespace Kudu.Core.Infrastructure {
    public static class FileSystemHelpers {
        public static void DeleteDirectorySafe(string path) {
            DoSafeAction(() => Directory.Delete(path, recursive: true));
        }

        public static string EnsureDirectory(string path) {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        private static void DoSafeAction(Action action) {
            try {
                Attempt(action);
            }
            catch {
            }
        }

        private static void Attempt(Action action, int retries = 3, int delayBeforeRetry = 150) {
            while (retries > 0) {
                try {
                    action();
                    break;
                }
                catch {
                    retries--;
                    if (retries == 0) {
                        throw;
                    }
                }
                Thread.Sleep(delayBeforeRetry);
            }
        }
    }
}
