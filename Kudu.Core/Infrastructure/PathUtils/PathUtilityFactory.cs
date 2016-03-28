using Kudu.Core.Helpers;

namespace Kudu.Core.Infrastructure
{
    public static class PathUtilityFactory
    {
        private static PathUtilityBase _utility = null;

        public static PathUtilityBase Instance
        {
            get
            {
                if (_utility == null)
                {
                    if (OSDetecter.IsOnWindows())
                    {
                        _utility = new PathWindowsUtility();
                    }
                    else
                    {
                        _utility = new PathLinuxUtility();
                    }
                }

                return _utility;
            }
        }
    }
}
