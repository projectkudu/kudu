using System;
using System.Collections.Generic;
using System.IO;
using Kudu.Client.SSHKey;

namespace Kudu.FunctionalTests
{
    public static class SshHelper
    {
        private const string SSHConfigFile = "config";
        private const string SSHKeyFile = "id_rsa";

        public static bool PrepareSSHEnv(RemoteSSHKeyManager sshManager)
        {
            string sshKey = ReadManifestFile(SSHKeyFile);
            if (!String.IsNullOrEmpty(sshKey))
            {
                sshManager.SetPrivateKey(sshKey).Wait();
                return true;
            }
            return false;
        }

        public static IDictionary<string, string> PrepareSSHEnv(out string id_rsa)
        {
            var sshPath = new DirectoryInfo(Path.Combine(Kudu.TestHarness.PathHelper.TestsRootPath, ".ssh"));
            id_rsa = WriteSSHKeys(sshPath);

            Dictionary<string, string> environments = new Dictionary<string, string>();
            environments["HOME"] = sshPath.Parent.FullName;
            environments["HOMEDRIVE"] = sshPath.Root.Name.Trim('\\');
            environments["HOMEPATH"] = environments["HOME"].Replace(environments["HOMEDRIVE"], String.Empty);
            return environments;
        }

        private static string WriteSSHKeys(DirectoryInfo sshPath)
        {
            if (!sshPath.Exists)
            {
                sshPath.Create();
            }
            string id_rsa = ReadManifestFile(SSHKeyFile);
            File.WriteAllText(Path.Combine(sshPath.FullName, SSHKeyFile), id_rsa);
            File.WriteAllText(Path.Combine(sshPath.FullName, SSHConfigFile), ReadManifestFile(SSHConfigFile));
            return id_rsa;
        }

        private static string ReadManifestFile(string fileName)
        {
            var assembly = typeof(SshHelper).Assembly;
            using (var reader = new StreamReader(assembly.GetManifestResourceStream("Kudu.FunctionalTests..ssh." + fileName)))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
