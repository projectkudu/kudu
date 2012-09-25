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
        public const string ConfigFile = "config";
        public const string ConfigContent = "HOST *\r\n  StrictHostKeyChecking no";
        private readonly IFileSystem _fileSystem;
        private readonly ITraceFactory _traceFactory;
        private readonly string _sshPath;
        private readonly string _id_rsa;
        private readonly string _config;

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
            _config = Path.Combine(sshPath, ConfigFile);
        }

        public void SetPrivateKey(string key)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("SSHKeyManager.SetPrivateKey"))
            {
                FileSystemHelpers.EnsureDirectory(_fileSystem, _sshPath);

                // bypass service key checking prompt (StrictHostKeyChecking=no).
                _fileSystem.File.WriteAllText(_config, ConfigContent);

                // This overrides if file exists
                _fileSystem.File.WriteAllText(_id_rsa, key);
            }
        }
    }
}