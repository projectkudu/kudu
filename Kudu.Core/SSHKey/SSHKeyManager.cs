using System;
using System.IO;
using System.Security.Cryptography;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SSHKey
{
    public class SSHKeyManager : ISSHKeyManager
    {
        private const string PrivateKeyFile = "id_rsa";
        private const string PublicKeyFile = "id_rsa.pub";
        private const string ConfigFile = "config";
        private const string ConfigContent = "HOST *\r\n  StrictHostKeyChecking no";
        private const int KeySize = 2048;
        private readonly ITraceFactory _traceFactory;
        private readonly string _sshPath;
        private readonly string _id_rsa;
        private readonly string _id_rsaPub;
        private readonly string _config;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _deploymentSettingManager;

        public SSHKeyManager(IEnvironment environment, ITraceFactory traceFactory, IDeploymentSettingsManager deploymentSettingManager)
        {
            if (environment == null)
            {
                throw new ArgumentNullException("environment");
            }

            _environment = environment;
            _traceFactory = traceFactory ?? NullTracerFactory.Instance;
            _deploymentSettingManager = deploymentSettingManager;
            _sshPath = environment.SSHKeyPath;
            _id_rsa = Path.Combine(_sshPath, PrivateKeyFile);
            _id_rsaPub = Path.Combine(_sshPath, PublicKeyFile);
            _config = Path.Combine(_sshPath, ConfigFile);
        }

        public void SetPrivateKey(string key)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("SSHKeyManager.SetPrivateKey"))
            {
                FileSystemHelpers.EnsureDirectory(_sshPath);

                // Delete existing public key
                if (FileSystemHelpers.FileExists(_id_rsaPub))
                {
                    FileSystemHelpers.DeleteFileSafe(_id_rsaPub);
                }

                // bypass service key checking prompt (StrictHostKeyChecking=no).
                FileSystemHelpers.WriteAllText(_config, ConfigContent);

                // This overrides if file exists
                FileSystemHelpers.WriteAllText(_id_rsa, key);
                EnsureFilePermission();
            }
        }

        /// <summary>
        /// Gets an existing created public key or creates a new one and returns the public key
        /// </summary>
        public string GetPublicKey(bool ensurePublicKey)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("SSHKeyManager.GetKey"))
            {
                if (FileSystemHelpers.FileExists(_id_rsaPub))
                {
                    tracer.Trace("Public key exists.");
                    // If a public key exists, return it.
                    return FileSystemHelpers.ReadAllText(_id_rsaPub);
                }
                else if (ensurePublicKey)
                {
                    tracer.Trace("Creating key pair.");
                    return CreateKeyPair();
                }

                // A public key does not exist but we weren't asked to create it. 
                return null;
            }
        }

        public void DeleteKeyPair()
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("SSHKeyManager.GetKey"))
            {
                // Delete public key
                FileSystemHelpers.DeleteFileSafe(_id_rsaPub);

                FileSystemHelpers.DeleteFileSafe(_id_rsa);
            }
        }

        private string CreateKeyPair()
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("SSHKeyManager.CreateKey"))
            {
                RSACryptoServiceProvider rsa = null;
                try
                {
                    rsa = new RSACryptoServiceProvider(dwKeySize: KeySize);
                    RSAParameters privateKeyParam = rsa.ExportParameters(includePrivateParameters: true);
                    RSAParameters publicKeyParam = rsa.ExportParameters(includePrivateParameters: false);

                    string privateKey = PEMEncoding.GetString(privateKeyParam);
                    string publicKey = SSHEncoding.GetString(publicKeyParam);

                    FileSystemHelpers.WriteAllText(_id_rsa, privateKey);
                    FileSystemHelpers.WriteAllText(_id_rsaPub, publicKey);

                    FileSystemHelpers.WriteAllText(_config, ConfigContent);
                    EnsureFilePermission();
                    return publicKey;
                }
                finally
                {
                    if (rsa != null)
                    {
                        rsa.PersistKeyInCsp = false;
                        rsa.Dispose();
                    }
                }
            }
        }

        private void EnsureFilePermission()
        {
            if (!OSDetector.IsOnWindows())
            {
                PermissionHelper.Chmod("600", _id_rsa, _environment, _deploymentSettingManager, NullLogger.Instance);
                PermissionHelper.Chmod("644", _id_rsaPub, _environment, _deploymentSettingManager, NullLogger.Instance);
            }
        }
    }
}