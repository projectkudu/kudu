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
            Tuple<string, string>[] keyPairs = SecurityUtility.GenerateSecretStringsKeyPair(2); // get a key pair <unencrypted, encrypted>
            string firstUnencryptedKey = keyPairs[0].Item1;
            string firstEncryptedKey = keyPairs[0].Item2;
            string secondEncryptedKey = keyPairs[1].Item2;

            Assert.Equal(firstUnencryptedKey, SecurityUtility.DecryptSecretString(firstEncryptedKey));
            Assert.NotEqual(firstUnencryptedKey, SecurityUtility.DecryptSecretString(secondEncryptedKey)); // decryption success, but value is wrong
            var exception = Assert.Throws<FormatException>(() => SecurityUtility.DecryptSecretString(firstUnencryptedKey)); // try to decrypt an unencrypted key will throw exception
            // wrapper around the following since we treate CryptographicException as 500 server error
            // Exceptions:
            //   T:System.Security.Cryptography.CryptographicException:
            //     Thrown if protectedData is invalid or malformed.
            // public static string Unprotect(this IDataProtector protector, string protectedData);
            Assert.Equal($"unable to decrypt {firstUnencryptedKey}, the key is either invalid or malformed", exception.Message);
        }
    }
}
