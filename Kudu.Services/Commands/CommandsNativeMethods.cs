using System.Runtime.InteropServices;


namespace Kudu.Services.Commands
{
    class CommandsNativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GenerateConsoleCtrlEvent(
            [In] ConsoleCtrlEvent sigevent,
            [In] uint processGroupId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachConsole(
            [In] uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetConsoleCtrlHandler(
            [In] HandlerRoutineDelegate handlerRoutine,
            [In, MarshalAs(UnmanagedType.Bool)] bool add);

        public delegate bool HandlerRoutineDelegate(ConsoleCtrlEvent consoleCtrlEvent);

        public enum ConsoleCtrlEvent
        {
            CTRL_C = 0
        }
    }
}
