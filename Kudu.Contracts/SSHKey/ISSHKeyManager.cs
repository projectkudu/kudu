
namespace Kudu.Core.SSHKey
{
    public interface ISSHKeyManager
    {
        /// <summary>
        /// Sets a private key
        /// </summary>
        void SetPrivateKey(string key);
        
        /// <summary>
        /// Reads an exisiting public key or generates a new key pair and returns it.
        /// </summary>
        /// <returns></returns>
        string GetKey();
        
        /// <summary>
        /// Create a new key pair overwritting any existing key files on disk.
        /// </summary>
        /// <returns></returns>
        string CreateKey();
    }
}
