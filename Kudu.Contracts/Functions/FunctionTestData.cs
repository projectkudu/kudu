namespace Kudu.Contracts.Functions
{
    public class FunctionTestData
    {
        // test shows test_data of size 8310000 bytes still delivers as an ARM package
        // whereas test_data of size 8388608 bytes fails
        public const long PackageMaxSizeInBytes = 8300000;

        public long BytesLeftInPackage { get; set; } = PackageMaxSizeInBytes;

        public bool DeductFromBytesLeftInPackage(long fileSize)
        {
            long spaceLeft = BytesLeftInPackage - fileSize;
            if (spaceLeft >= 0)
            {
                BytesLeftInPackage = spaceLeft;
                return true;
            }
            return false;
        }
    }
}
