using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    internal delegate bool ConsoleCtrlHandler(int dwCtrlType);

    [DllImport("kernel32.dll")]
    internal static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handler, bool add);

    // HID output via the control pipe — works for both USB and BT where
    // interrupt-pipe WriteFile fails (common with Bluetooth HID on Windows).
    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_SetOutputReport(
        SafeFileHandle hidDeviceObject,
        byte[]         lpReportBuffer,
        int            reportBufferLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WriteFile(
        SafeFileHandle hFile,
        byte[]         lpBuffer,
        int            nNumberOfBytesToWrite,
        out int        lpNumberOfBytesWritten,
        nint           lpOverlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(
        string  lpFileName,
        uint    dwDesiredAccess,
        uint    dwShareMode,
        nint    lpSecurityAttributes,
        uint    dwCreationDisposition,
        uint    dwFlagsAndAttributes,
        nint    hTemplateFile);

    internal const uint GENERIC_READ       = 0x80000000;
    internal const uint GENERIC_WRITE      = 0x40000000;
    internal const uint FILE_SHARE_READ    = 0x00000001;
    internal const uint FILE_SHARE_WRITE   = 0x00000002;
    internal const uint OPEN_EXISTING      = 3;
    internal const uint FILE_FLAG_OVERLAPPED = 0x40000000;
}
