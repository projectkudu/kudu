using System;

namespace Kudu.Contracts
{
    public static class HashHelpers
    {
        // Source: http://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-an-overridden-system-object-gethashcode
        public static int CalculateCompositeHash(params object[] args)
        {
            if (args.Length == 1)
            {
                return (args[0] ?? String.Empty).GetHashCode();
            }

            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;

                foreach (object arg in args)
                {
                    hash = hash * 23 + (arg ?? String.Empty).GetHashCode();
                }

                return hash;
            }
        }
    }
}