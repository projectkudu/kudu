using System;
using System.IO;
using System.IO.Abstractions;
using System.Security.Cryptography;
using Kudu.Contracts.Tracing;
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
        private readonly IFileSystem _fileSystem;
        private readonly ITraceFactory _traceFactory;
        private readonly string _sshPath;
        private readonly string _id_rsa;
        private readonly string _id_rsaPub;
        private readonly string _config;

        public SSHKeyManager(IEnvironment environment, IFileSystem fileSystem, ITraceFactory traceFactory)
        {
            if (environment == null)
            {
                throw new ArgumentNullException("environment");
            }

            if (fileSystem == null)
            {
                throw new ArgumentNullException("fileSystem");
            }

            _fileSystem = fileSystem;
            _traceFactory = traceFactory ?? NullTracerFactory.Instance;
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
                RSAParameters publicKeyParameters = PEMEncoding.ExtractPublicKey(key);
                string publicKey = SSHEncoding.GetString(publicKeyParameters);

                FileSystemHelpers.EnsureDirectory(_fileSystem, _sshPath);

                // bypass service key checking prompt (StrictHostKeyChecking=no).
                _fileSystem.File.WriteAllText(_config, ConfigContent);

                // This overrides if file exists
                _fileSystem.File.WriteAllText(_id_rsa, key);

                _fileSystem.File.WriteAllText(_id_rsaPub, publicKey);
            }
        }

        /// <summary>
        /// Gets an existing created public key or creates a new one and returns the public key
        /// </summary>
        public string GetKey()
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("SSHKeyManager.GetKey"))
            {
                if (_fileSystem.File.Exists(_id_rsaPub))
                {
                    tracer.Trace("Public key exists.");
                    // If a public key exists, return it.
                    return _fileSystem.File.ReadAllText(_id_rsaPub);
                }

                if (_fileSystem.File.Exists(_id_rsa))
                {
                    tracer.Trace("Private key exists without public key.");
                    // If a private key exists without a public key, extract the public key to disk and return it.
                    // This might occur in back-compat scenarios where we've an existing private key from pre S21 scenarios.
                    string privateKey = _fileSystem.File.ReadAllText(_id_rsa);
                    RSAParameters publicKeyParameters = PEMEncoding.ExtractPublicKey(privateKey);

                    string publicKey = SSHEncoding.GetString(publicKeyParameters);
                    _fileSystem.File.WriteAllText(_id_rsaPub, publicKey);

                    return publicKey;
                }

                tracer.Trace("Generating SSH key pair.");
                // Neither exists. Proceed to create a new key-pair.
                return CreateKey();
            }
        }

        public string CreateKey()
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

                    _fileSystem.File.WriteAllText(_id_rsa, privateKey);
                    _fileSystem.File.WriteAllText(_id_rsaPub, publicKey);

                    _fileSystem.File.WriteAllText(_config, ConfigContent);

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
    }
}