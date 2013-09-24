using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private static Dictionary<byte, string> _rawTypeMap = new Dictionary<byte, string>();
        private string _name;
        private string _typeStr;
        private HandleType _type;
        
        public int ProcessId { get; private set; }
        public ushort Handle { get; private set; }
        public int GrantedAccess { get; private set; }
        public byte RawType { get; private set; }

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
                     
            try
            {
                // Query the object type
                if (_rawTypeMap.ContainsKey(RawType))
                    _typeStr = _rawTypeMap[RawType];
                else
                {
                    int length;
                    NativeMethods.NtQueryObject(
                        (IntPtr)Handle, 
                        OBJECT_INFORMATION_CLASS.ObjectTypeInformation, 
                        IntPtr.Zero, 0, out length);

                    IntPtr ptr = IntPtr.Zero;
                    try
                    {
                        ptr = Marshal.AllocHGlobal(length);
                        if (NativeMethods.NtQueryObject(
                            (IntPtr)Handle,
                            OBJECT_INFORMATION_CLASS.ObjectTypeInformation,
                            ptr, length, out length) != NT_STATUS.STATUS_SUCCESS)
                        {
                            return;
                        }
                        _typeStr = Marshal.PtrToStringUni((IntPtr)((int)ptr + 0x58 + 2 * IntPtr.Size));
                        _rawTypeMap[RawType] = _typeStr;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
                _type = HandleTypeFromString(_typeStr);
             
                if (_typeStr != null && GrantedAccess != 0x0012019f && GrantedAccess != 0x00120189 && GrantedAccess != 0x120089) // don't query some objects that could get stuck
                {
                    int length;
                    NativeMethods.NtQueryObject(
                        (IntPtr)Handle, 
                        OBJECT_INFORMATION_CLASS.ObjectNameInformation, 
                        IntPtr.Zero, 0, out length);

                    IntPtr ptr = IntPtr.Zero;
                    try
                    {
                        ptr = Marshal.AllocHGlobal(length);
                        if (NativeMethods.NtQueryObject(
                            (IntPtr)Handle,
                            OBJECT_INFORMATION_CLASS.ObjectNameInformation,
                            ptr, length, out length) != NT_STATUS.STATUS_SUCCESS)
                        {
                            return;
                        }
                        _name = Marshal.PtrToStringUni((IntPtr)((int)ptr + 2 * IntPtr.Size));
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
            }
            finally
            {                
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
