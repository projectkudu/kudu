using Kudu.Core.Infrastructure;
using System;
using System.Security.Cryptography;
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
            // try to decrypt an invalid encryptedkey will throw an error
            string malformattedKey = encryptedKey.Substring(1);
            var exception = Assert.Throws<FormatException>(() => SecurityUtility.DecryptSecretString(malformattedKey));

            // map CryptographicException to FormatException so that HTTP return 400 instead of 500
            Assert.Equal($"unable to decrypt {malformattedKey}, the key is either invalid or malformed", exception.Message);
            Assert.Equal<Type>(typeof(CryptographicException), exception.InnerException.GetType());
        }
    }
}
