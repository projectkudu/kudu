
namespace Kudu.Core.SSHKey
{
    public interface ISSHKeyManager
    {
        /// <summary>
        /// Sets a private key
        /// </summary>
        void SetPrivateKey(string key);
        
        /// <summary>
        /// Reads an exisiting public key or generates a new key pair.
        /// </summary>
        /// <param name="ensurePublicKey">Determines if a public key should be generated if it doesn't already exist</param>
        string GetPublicKey(bool ensurePublicKey);
        
        /// <summary>
        /// Deletes the key pair
        /// </summary>
        /// <returns></returns>
        void DeleteKeyPair();
    }
}
