using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace UniversalSensRandomizer.Interop;

internal static partial class RawAccelInterop
{
    public const string DevicePath = @"\\.\rawaccel";

    public const uint IoctlRead = 0x88882220;
    public const uint IoctlWrite = 0x88882224;
    public const uint IoctlGetVersion = 0x88882228;

    public const uint GenericRead = 0x80000000;
    public const uint GenericWrite = 0x40000000;
    public const uint OpenExisting = 3;

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFileNative(
        string filename,
        uint access,
        uint share,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flags,
        IntPtr template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        void* inBuffer,
        uint inBufferSize,
        void* outBuffer,
        uint outBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);

    public static SafeFileHandle Open()
    {
        SafeFileHandle handle = CreateFileNative(DevicePath, 0, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException($"Could not open RawAccel device {DevicePath} (Win32 error {error}). Is the driver installed?", new Win32Exception(error));
        }
        return handle;
    }

    public static unsafe uint Read(SafeFileHandle handle, byte[] outBuffer)
    {
        fixed (byte* outPtr = outBuffer)
        {
            bool ok = DeviceIoControl(handle, IoctlRead, null, 0, outPtr, (uint)outBuffer.Length, out uint bytesReturned, IntPtr.Zero);
            if (!ok)
            {
                int error = Marshal.GetLastWin32Error();
                throw new IOException($"DeviceIoControl READ failed (Win32 error {error}).", new Win32Exception(error));
            }
            return bytesReturned;
        }
    }

    public static unsafe void Write(SafeFileHandle handle, byte[] inBuffer)
    {
        fixed (byte* inPtr = inBuffer)
        {
            bool ok = DeviceIoControl(handle, IoctlWrite, inPtr, (uint)inBuffer.Length, null, 0, out _, IntPtr.Zero);
            if (!ok)
            {
                int error = Marshal.GetLastWin32Error();
                throw new IOException($"DeviceIoControl WRITE failed (Win32 error {error}).", new Win32Exception(error));
            }
        }
    }
}
