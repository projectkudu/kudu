using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SSHKey
{
    public class SSHKeyManager : ISSHKeyManager
    {
        public const string PrivateKeyFile = "id_rsa";

        private readonly IFileSystem _fileSystem;
        private readonly ITraceFactory _traceFactory;
        private readonly string _sshPath;
        private readonly string _id_rsa;

        public SSHKeyManager(IEnvironment environment, IFileSystem fileSystem, ITraceFactory traceFactory)
            : this(fileSystem, traceFactory, environment.SSHKeyPath)
        {
            _fileSystem = fileSystem;
            _traceFactory = traceFactory;
        }

        protected SSHKeyManager(IFileSystem fileSystem, ITraceFactory traceFactory, string sshPath)
        {
            _fileSystem = fileSystem;
            _traceFactory = traceFactory;
            _sshPath = sshPath;
            _id_rsa = Path.Combine(sshPath, PrivateKeyFile);
        }

        public void SetPrivateKey(string key, bool overwrite)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("SSHKeyManager.SetPrivateKey"))
            {
                if (_fileSystem.File.Exists(_id_rsa) && !overwrite)
                {
                    string pem = _fileSystem.File.ReadAllText(_id_rsa);
                    if (!string.Equals(key, pem))
                    {
                        throw new InvalidOperationException("PEM key file already exist!");
                    }
                }

                FileSystemHelpers.EnsureDirectory(_fileSystem, _sshPath);

                // This overrides if file exists
                _fileSystem.File.WriteAllText(_id_rsa, key);
            }
        }
    }
}