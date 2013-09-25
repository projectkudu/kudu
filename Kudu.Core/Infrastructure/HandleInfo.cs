using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Kudu.Core.Infrastructure
{
    public enum HandleType
    {
        Unknown,
        Other,
        File, Directory, SymbolicLink, Key,
        Process, Thread, Job, Session, WindowStation,
        Timer, Desktop, Semaphore, Token,
        Mutant, Section, Event, KeyedEvent, IoCompletion, IoCompletionReserve,
        TpWorkerFactory, AlpcPort, WmiGuid, UserApcReserve,
    }

    public class HandleInfo
    {
        private const string networkDevicePrefix = "\\Device\\LanmanRedirector\\";
        private const int MAX_PATH = 260;
        private const int PROCESS_DUP_HANDLE = 0x40;

        private static Dictionary<string, string> deviceMap;
        private static Dictionary<byte, string> _rawTypeMap = new Dictionary<byte, string>();
        private string _name;
        private string _dosFilePath;
        private string _typeStr;
        private HandleType _type;
        
        public int ProcessId { get; private set; }
        public ushort Handle { get; private set; }
        public int GrantedAccess { get; private set; }
        public byte RawType { get; private set; }

        public string DosFilePath
        {
            get
            {
                if (_dosFilePath == null)
                {
                    initDosFilePath();
                }
                return _dosFilePath;
            }
        }

        public string Name 
        { 
            get 
            {
                if (_name == null)
                {
                    initTypeAndName();
                }
                return _name; 
            } 
        }

        public string TypeString
        {
            get
            {
                if (_typeStr == null)
                {
                    initType();
                }
                return _typeStr;
            }
        }

        public HandleType Type 
        { 
            get 
            {
                if (_typeStr == null)
                {
                    initType();
                } 
                return _type;
            }
        }

        public HandleInfo(int processId, ushort handle, int grantedAccess, byte rawType)
        {
            ProcessId = processId;
            Handle = handle;
            GrantedAccess = grantedAccess;
            RawType = rawType;
        }        

        private void initDosFilePath()
        {
            EnsureDeviceMap();

            if (Name != null)
            {
                int i = Name.Length;
                while (i > 0 && (i = Name.LastIndexOf('\\', i - 1)) != -1)
                {
                    string drive;
                    if (deviceMap.TryGetValue(Name.Substring(0, i), out drive))
                    {
                        _dosFilePath = string.Concat(drive, Name.Substring(i));
                    }
                }
            }
        }

        private static void EnsureDeviceMap()
        {
            if (deviceMap == null)
            {
                Dictionary<string, string> localDeviceMap = BuildDeviceMap();
                Interlocked.CompareExchange<Dictionary<string, string>>(ref deviceMap, localDeviceMap, null);
            }
        }

        private static Dictionary<string, string> BuildDeviceMap()
        {
            string[] logicalDrives = System.Environment.GetLogicalDrives();
            Dictionary<string, string> localDeviceMap = new Dictionary<string, string>(logicalDrives.Length);
            StringBuilder lpTargetPath = new StringBuilder(MAX_PATH);
            foreach (string drive in logicalDrives)
            {
                string lpDeviceName = drive.Substring(0, 2);
                FileHandleNativeMethods.QueryDosDevice(lpDeviceName, lpTargetPath, MAX_PATH);
                localDeviceMap.Add(NormalizeDeviceName(lpTargetPath.ToString()), lpDeviceName);
            }
            localDeviceMap.Add(networkDevicePrefix.Substring(0, networkDevicePrefix.Length - 1), "\\");
            return localDeviceMap;
        }

        private static string NormalizeDeviceName(string deviceName)
        {
            if (string.Compare(deviceName, 0, networkDevicePrefix, 0, networkDevicePrefix.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                string shareName = deviceName.Substring(deviceName.IndexOf('\\', networkDevicePrefix.Length) + 1);
                return string.Concat(networkDevicePrefix, shareName);
            }
            return deviceName;
        }

        private void initType()
        {
            if (_rawTypeMap.ContainsKey(RawType))
            {
                _typeStr = _rawTypeMap[RawType];
                _type = HandleTypeFromString(_typeStr);
            }
            else
                initTypeAndName();
        }

        bool _typeAndNameAttempted = false;

        private void initTypeAndName()
        {
            if (_typeAndNameAttempted)
                return;
            _typeAndNameAttempted = true;
            IntPtr sourceProcessHandle = IntPtr.Zero;
            IntPtr handleDuplicate = IntPtr.Zero;
        
            try
            {

                sourceProcessHandle = FileHandleNativeMethods.OpenProcess(PROCESS_DUP_HANDLE, true, ProcessId);

                // To read info about a handle owned by another process we must duplicate it into ours
                // For simplicity, current process handles will also get duplicated; remember that process handles cannot be compared for equality
                if (!FileHandleNativeMethods.DuplicateHandle(
                    sourceProcessHandle, 
                    (IntPtr)Handle, 
                    FileHandleNativeMethods.GetCurrentProcess(), 
                    out handleDuplicate, 0, false, 2 /* same_access */))

                    return;

                // Query the object type
                if (_rawTypeMap.ContainsKey(RawType))
                    _typeStr = _rawTypeMap[RawType];
                else
                {
                    int length;
                    FileHandleNativeMethods.NtQueryObject(
                        handleDuplicate, 
                        OBJECT_INFORMATION_CLASS.ObjectTypeInformation, 
                        IntPtr.Zero, 0, out length);

                    IntPtr ptr = IntPtr.Zero;
                    try
                    {
                        ptr = Marshal.AllocHGlobal(length);
                        if (FileHandleNativeMethods.NtQueryObject(
                            handleDuplicate,
                            OBJECT_INFORMATION_CLASS.ObjectTypeInformation,
                            ptr, length, out length) != NT_STATUS.STATUS_SUCCESS)
                        {
                            return;
                        }

                        int offset = 0x58 + 2 * IntPtr.Size;
                        _typeStr = Marshal.PtrToStringUni((IntPtr)(IntPtr.Add(ptr, offset)));
                        _rawTypeMap[RawType] = _typeStr;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
                _type = HandleTypeFromString(_typeStr);

                // don't query some objects that could get stuck
                /*if (_typeStr != null && GrantedAccess != 0x0012019f 
                    && GrantedAccess != 0x00120189 && GrantedAccess != 0x120089)                */
                if(_typeStr != null)
                {
                    int length;
                    /*
                    var fileStructure = new FileHandleNativeMethods.FILE_ID_BOTH_DIR_INFO();
                    if(FileHandleNativeMethods.GetFileInformationByHandleEx(
                        handleDuplicate, 
                        FileHandleNativeMethods.FILE_INFO_BY_HANDLE_CLASS.FileIdBothDirectoryInfo, 
                        out fileStructure, 
                        (uint)Marshal.SizeOf(fileStructure)))
                    {
                        _name = fileStructure.ShortName;
                    }*/
                    
                    FileHandleNativeMethods.NtQueryObject(
                        handleDuplicate, 
                        OBJECT_INFORMATION_CLASS.ObjectNameInformation, 
                        IntPtr.Zero, 0, out length);

                    IntPtr ptr = IntPtr.Zero;
                    try
                    {
                        ptr = Marshal.AllocHGlobal(length);
                        if (FileHandleNativeMethods.NtQueryObject(
                            handleDuplicate,
                            OBJECT_INFORMATION_CLASS.ObjectNameInformation,
                            ptr, length, out length) != NT_STATUS.STATUS_SUCCESS)
                        {
                            return;
                        }
                       _name = Marshal.PtrToStringUni(IntPtr.Add(ptr, 2 * IntPtr.Size));
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
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
