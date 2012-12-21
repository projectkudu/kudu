using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Kudu.FunctionalTests
{
    public static class SshHelper
    {
        public static string PrepareSSHEnv(string siteRoot)
        {
            var sshPath = new DirectoryInfo(Path.Combine(siteRoot, ".ssh"));
            return WriteSSHKeys(sshPath);
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
            string id_rsa = null;
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (var fileName in new string[] { "config", "id_rsa" })
            {
                using (var reader = new StreamReader(assembly.GetManifestResourceStream("Kudu.FunctionalTests..ssh." + fileName)))
                {
                    using (var writer = new StreamWriter(new FileStream(Path.Combine(sshPath.FullName, fileName), FileMode.Create, FileAccess.Write)))
                    {
                        string content = reader.ReadToEnd();
                        if (fileName == "id_rsa")
                        {
                            id_rsa = content;
                        }
                        writer.Write(content);
                    }
                }
            }
            return id_rsa;
        }
    }
}
