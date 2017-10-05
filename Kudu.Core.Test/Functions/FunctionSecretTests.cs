using Kudu.Core.Infrastructure;
using System;
using Xunit;

namespace Kudu.Core.Test.Functions
{
    public class FunctionSecretTests
    {
        [Fact]
        public void DecryptEncryptedKeyTests()
        {
            // get a key pair <unencrypted, encrypted>
            Tuple<string, string>[] keyPairs = SecurityUtility.GenerateSecretStringsKeyPair(1);
            string unencryptedKey = keyPairs[0].Item1;
            string encryptedKey = keyPairs[0].Item2;

            Assert.Equal(unencryptedKey, SecurityUtility.DecryptSecretString(encryptedKey));
            var exception = Assert.Throws<FormatException>(() => SecurityUtility.DecryptSecretString(unencryptedKey)); // try to decrypt an unencrypted key will throw exception

            // map CryptographicException to FormatException so that HTTP return 400 instead of 500
            Assert.Equal($"unable to decrypt {unencryptedKey}, the key is either invalid or malformed", exception.Message);
        }
    }
}
