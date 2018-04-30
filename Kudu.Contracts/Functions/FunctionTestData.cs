namespace Kudu.Contracts.Functions
{
    public class FunctionTestData
    {
        // ARM has a limit of 8 MB -> 8388608 bytes
        // divid by 2 to limit the over all size of test data to half of arm requirement to be safe.
        public const long PackageMaxSizeInBytes = 8388608 / 2;

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