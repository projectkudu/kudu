namespace Kudu.Contracts.Functions
{
    public class FunctionTestData
    {
        public static readonly long packageMaxSizeInBytes = 8300000;
        // test shows test_data of size 8310000 bytes still delivers as an ARM package
        // whereas test_data of size 8388608 bytes fails

        private long remain;

        public FunctionTestData()
        {
            remain = packageMaxSizeInBytes;
        }

        public long spaceLeftinPackage
        {
            get
            {
                return remain;
            }
        }

        public bool canWrite(long fileSize)
        {
            long spaceLeft = remain - fileSize;
            if (spaceLeft >= 0)
            {
                remain = spaceLeft;
                return true;
            }
            return false;
        }
    }
}
