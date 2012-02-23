using System.IO;

namespace Kudu.Core.Infrastructure
{
    internal static class ExecutableExtensions
    {
        private const string AppData = "APPDATA";
        private const string LocalAppData = "LOCALAPPDATA";
        private const string UserProfile = "USERPROFILE";

        /// <summary>
        /// Remap profile folders under some root directory.
        /// </summary>
        public static void MapProfiles(this Executable exe, string root)
        {
            string appData = Path.Combine(root, "AppData");
            string localAppData = Path.Combine(root, "LocalAppData");
            string userProfile = Path.Combine(root, "Profile");

            exe.EnvironmentVariables[AppData] = FileSystemHelpers.EnsureDirectory(appData);
            exe.EnvironmentVariables[LocalAppData] = FileSystemHelpers.EnsureDirectory(localAppData);
            exe.EnvironmentVariables[UserProfile] = FileSystemHelpers.EnsureDirectory(userProfile);
        }
    }
}
