using System;

namespace Kudu.Core.SSHKey
{
    public interface ISSHKeyManager
    {
        void SetPrivateKey(string key);
    }
}
