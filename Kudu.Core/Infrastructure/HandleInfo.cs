using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Kudu.Core.Infrastructure
{
    public enum HandleType
    {
        Unknown,
        Other,
        File,
        Directory
    }

    public class HandleInfo
    {
        /*
         * Ideally we will grab all the Network providers from HKLM\SYSTEM\CurrentControlSet\Control\NetworkProvider\Order
         * and look for their Device names under HKLM\SYSTEM\CurrentControlSet\Services\<NetworkProviderName>\NetworkProvider\DeviceName
         * http://msdn.microsoft.com/en-us/library/windows/hardware/ff550865%28v=vs.85%29.aspx
         * However, these providers are generally for devices that are not supported on Azure, so there is no value in adding them.
        */

        private const string NetworkDevicePrefix = "\\Device\\Mup\\";

        private const string NetworkPrefix = "\\\\";

        private const string SiteWwwroot = "SITE\\WWWROOT";

        private const int MaxPath = 260;

        private readonly TimeSpan NtQueryObjectTimeout = TimeSpan.FromMilliseconds(50);

        public uint ProcessId { get; private set; }

        public ushort Handle { get; private set; }

        public int GrantedAccess { get; private set; }

        public byte RawType { get; private set; }

        private static Dictionary<byte, string> _rawTypeMap;

        private static Dictionary<byte, string> RawTypeMap
        {
            get
            {
                return _rawTypeMap ?? (_rawTypeMap = new Dictionary<byte, string>());
            }
        }

        private string _dosFilePath;

        public string DosFilePath
        {
            get
            {
                if (_dosFilePath == null)
                {
                    InitDosFilePath();
                }
                return _dosFilePath;
            }
        }

        private string _name;

        public string Name
        {
            get
            {
                if (_name == null)
                {
                    InitTypeAndName();
                }
                return _name;
            }

            private set
            {
                _name = value;
            }
        }

        private string _typeString;

        public string TypeString
        {
            get
            {
                if (_typeString == null)
                {
                    InitType();
                }
                return _typeString;
            }

            private set
            {
                _typeString = value;
            }
        }

        public HandleType Type 
        { 
            get
            {
                return HandleTypeFromString(TypeString);
            }
        }

        private static string _homePath;

        public static string HomePath
        {
            get
            {
                return _homePath ?? (_homePath = System.Environment.ExpandEnvironmentVariables("%HOME%"));
            }
        }

        private static string _uncPath;

        public static string UncPath
        {
            get
            {
                if (_uncPath != null)
                    return _uncPath;
                var wwwrootHandle = FileHandleNativeMethods.CreateFile(Path.Combine(HomePath, SiteWwwroot),
                                                                       FileAccess.Read,
                                                                       FileShare.ReadWrite,
                                                                       IntPtr.Zero,
                                                                       FileMode.Open,
                                                                       FileFlagsAndAttributes.FileFlagBackupSemantics,
                                                                       IntPtr.Zero);
                var wwwrootPath = GetNameFromHandle(wwwrootHandle);
                _uncPath = Regex.Replace(wwwrootPath, Regex.Escape("\\" + SiteWwwroot), String.Empty, RegexOptions.IgnoreCase);
                _uncPath = Regex.Replace(_uncPath, Regex.Escape(NetworkDevicePrefix), NetworkPrefix, RegexOptions.IgnoreCase);
                return _uncPath;
            }
        }

        private static Dictionary<string, string> _deviceMap;

        private static Dictionary<string, string> DeviceMap
        {
            get { return _deviceMap ?? (_deviceMap = BuildDeviceMap()); }
        }

        public HandleInfo(uint processId, ushort handle, int grantedAccess, byte rawType)
        {
            ProcessId = processId;
            Handle = handle;
            GrantedAccess = grantedAccess;
            RawType = rawType;
        }

        private void InitDosFilePath()
        {
            if (Name != null)
            {
                int i = Name.Length;
                while (i > 0 && (i = Name.LastIndexOf('\\', i - 1)) != -1)
                {
                    string drive;
                    if (DeviceMap.TryGetValue(Name.Substring(0, i), out drive))
                    {
                        _dosFilePath = string.Concat(drive, Name.Substring(i));
                        _dosFilePath = Regex.Replace(_dosFilePath, Regex.Escape(UncPath), HomePath,
                            RegexOptions.IgnoreCase);
                    }
                }
            }
        }

        private static Dictionary<string, string> BuildDeviceMap()
        {
            var logicalDrives = System.Environment.GetLogicalDrives();
            var localDeviceMap = new Dictionary<string, string>(logicalDrives.Length);
            var lpTargetPath = new StringBuilder(MaxPath);
            foreach (string drive in logicalDrives)
            {
                string lpDeviceName = drive.Substring(0, 2);
                FileHandleNativeMethods.QueryDosDevice(lpDeviceName, lpTargetPath, MaxPath);
                localDeviceMap.Add(NormalizeDeviceName(lpTargetPath.ToString()), lpDeviceName);
            }
            localDeviceMap.Add(NetworkDevicePrefix.Substring(0, NetworkDevicePrefix.Length - 1), "\\");
            return localDeviceMap;
        }

        private static string NormalizeDeviceName(string deviceName)
        {
            if (string.Compare(deviceName, 0, NetworkDevicePrefix, 0, NetworkDevicePrefix.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                string shareName = deviceName.Substring(deviceName.IndexOf('\\', NetworkDevicePrefix.Length) + 1);
                return string.Concat(NetworkDevicePrefix, shareName);
            }
            return deviceName;
        }

        private void InitType()
        {
            if (RawTypeMap.ContainsKey(RawType))
            {
                TypeString = RawTypeMap[RawType];
            }
            else
            {
                InitTypeAndName();
            }
        }

        bool _typeAndNameAttempted;

        private void InitTypeAndName()
        {
           if (_typeAndNameAttempted)
                return;
            _typeAndNameAttempted = true;
            IntPtr sourceProcessHandle = IntPtr.Zero;
            IntPtr handleDuplicate = IntPtr.Zero;

            try
            {

                sourceProcessHandle = FileHandleNativeMethods.OpenProcess(ProcessAccessRights.ProcessDupHandle, true,
                    ProcessId);

                // To read info about a handle owned by another process we must duplicate it into ours
                // For simplicity, current process handles will also get duplicated; remember that process handles cannot be compared for equality
                if (!FileHandleNativeMethods.DuplicateHandle(sourceProcessHandle,
                    (IntPtr) Handle,
                    FileHandleNativeMethods.GetCurrentProcess(),
                    out handleDuplicate,
                    0,
                    false,
                    DuplicateHandleOptions.DuplicateSameAccess))
                {
                    return;
                }

                // Query the object type
                if (RawTypeMap.ContainsKey(RawType))
                {
                    TypeString = RawTypeMap[RawType];
                }
                else
                {
                    uint length;
                    FileHandleNativeMethods.NtQueryObject(handleDuplicate,
                        ObjectInformationClass.ObjectTypeInformation,
                        IntPtr.Zero,
                        0,
                        out length);

                    IntPtr ptr = IntPtr.Zero;
                    try
                    {
                        ptr = Marshal.AllocHGlobal((int) length);
                        if (FileHandleNativeMethods.NtQueryObject(handleDuplicate,
                            ObjectInformationClass.ObjectTypeInformation,
                            ptr,
                            length,
                            out length) != NtStatus.StatusSuccess)
                        {
                            return;
                        }

                        var typeInformation = (ObjectTypeInformation) Marshal.PtrToStructure(ptr, typeof (ObjectTypeInformation));
                        TypeString = typeInformation.Name.ToString();
                        RawTypeMap[RawType] = TypeString;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
                /*
                 * NtQueryObject can hang if called on a synchronous handle that is blocked on an operation (usually a read on pipes, but true for any synchronous handle)
                 * The process can also have handles over the network which might be slow to resolve.
                 * Therefore, I think having a timeout on the NtQueryObject is the correct approach. Timeout is currently 50 msec.
                 */
                ExecuteWithTimeout(() => { Name = GetNameFromHandle(handleDuplicate); }, NtQueryObjectTimeout);
            }
            catch (Exception e)
            {
                Console.Write(e);
            }
            finally
            {
                FileHandleNativeMethods.CloseHandle(sourceProcessHandle);
                if (handleDuplicate != IntPtr.Zero)
                {
                    FileHandleNativeMethods.CloseHandle(handleDuplicate);
                }
            }
        }

        private static string GetNameFromHandle(IntPtr handle)
        {
            uint length;

            FileHandleNativeMethods.NtQueryObject(
                handle,
                ObjectInformationClass.ObjectNameInformation,
                IntPtr.Zero, 0, out length);
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal((int) length);
                if (FileHandleNativeMethods.NtQueryObject(
                    handle,
                    ObjectInformationClass.ObjectNameInformation,
                    ptr, length, out length) != NtStatus.StatusSuccess)
                {
                    return null;
                }
                var unicodeStringName = (UnicodeString) Marshal.PtrToStructure(ptr, typeof (UnicodeString));
                return unicodeStringName.ToString();
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static void ExecuteWithTimeout(Action action, TimeSpan timeout)
        {
            var cancellationToken = new CancellationTokenSource();
            var task = new Task(action, cancellationToken.Token);
            task.Start();
            if (task.Wait(timeout))
                return;
            cancellationToken.Cancel(false);
        }

        public static HandleType HandleTypeFromString(string typeStr)
        {
            switch (typeStr)
            {
                case null: 
                    return HandleType.Unknown;
                case "File": 
                    return HandleType.File;
                case "Directory": 
                    return HandleType.Directory;
                default: 
                    return HandleType.Other;
            }
        }
    }
}
