using System;
using System.Runtime.InteropServices;
using System.Threading;
using UniversalSensRandomizer.Models;

namespace UniversalSensRandomizer.Interop;

public sealed class MessageOnlyWindow : IDisposable
{
    private const string ClassName = "UniversalSensRandomizerHotkeyWindow";

    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new(false);
    private readonly Win32Interop.WndProcDelegate wndProcDelegate;
    private readonly Win32Interop.HookProcDelegate hookProcDelegate;

    private IntPtr windowHandle;
    private IntPtr hookHandle;
    private uint targetVirtualKey;
    private HotkeyModifiers targetModifiers;
    private bool disposed;

    public event Action? HotkeyPressed;

    public MessageOnlyWindow()
    {
        wndProcDelegate = WndProc;
        hookProcDelegate = HookProc;
        thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "UniversalSensRandomizer.Hotkey",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
    }

    public void RequestRegister(HotkeyCombination combo)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }
        IntPtr packed = (IntPtr)(((long)combo.VirtualKey << 32) | (long)combo.Modifiers);
        Win32Interop.PostMessage(windowHandle, Win32Interop.WM_USER_REGISTER, packed, IntPtr.Zero);
    }

    public void RequestUnregister()
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }
        Win32Interop.PostMessage(windowHandle, Win32Interop.WM_USER_UNREGISTER, IntPtr.Zero, IntPtr.Zero);
    }

    private void ThreadMain()
    {
        IntPtr hInstance = Win32Interop.GetModuleHandle(null);
        IntPtr wndProcPtr = Marshal.GetFunctionPointerForDelegate(wndProcDelegate);

        IntPtr classNamePtr = Marshal.StringToHGlobalUni(ClassName);
        try
        {
            Win32Interop.WNDCLASSEX wcx = new()
            {
                cbSize = (uint)Marshal.SizeOf<Win32Interop.WNDCLASSEX>(),
                lpfnWndProc = wndProcPtr,
                hInstance = hInstance,
                lpszClassName = classNamePtr,
            };

            ushort atom = Win32Interop.RegisterClassEx(wcx);
            if (atom == 0)
            {
                ready.Set();
                return;
            }

            windowHandle = Win32Interop.CreateWindowEx(
                0, ClassName, null, 0, 0, 0, 0, 0,
                Win32Interop.HWND_MESSAGE, IntPtr.Zero, hInstance, IntPtr.Zero);

            if (windowHandle == IntPtr.Zero)
            {
                ready.Set();
                return;
            }

            IntPtr hookProcPtr = Marshal.GetFunctionPointerForDelegate(hookProcDelegate);
            hookHandle = Win32Interop.SetWindowsHookEx(Win32Interop.WH_KEYBOARD_LL, hookProcPtr, hInstance, 0);

            ready.Set();

            while (Win32Interop.GetMessage(out Win32Interop.MSG msg, IntPtr.Zero, 0, 0) > 0)
            {
                Win32Interop.TranslateMessage(msg);
                Win32Interop.DispatchMessage(msg);
            }
        }
        finally
        {
            if (hookHandle != IntPtr.Zero)
            {
                Win32Interop.UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
            Marshal.FreeHGlobal(classNamePtr);
        }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == Win32Interop.HC_ACTION)
        {
            int message = wParam.ToInt32();
            if (message == Win32Interop.WM_KEYDOWN || message == Win32Interop.WM_SYSKEYDOWN)
            {
                Win32Interop.KBDLLHOOKSTRUCT data = Marshal.PtrToStructure<Win32Interop.KBDLLHOOKSTRUCT>(lParam);
                uint vk = data.vkCode;
                if (targetVirtualKey != 0 && vk == targetVirtualKey && ModifiersMatch(targetModifiers))
                {
                    HotkeyPressed?.Invoke();
                }
            }
        }
        return Win32Interop.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static bool ModifiersMatch(HotkeyModifiers required)
    {
        bool ctrl = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_CONTROL) & 0x8000) != 0;
        bool alt = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_MENU) & 0x8000) != 0;
        bool shift = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_SHIFT) & 0x8000) != 0;
        bool win = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_LWIN) & 0x8000) != 0
                || (Win32Interop.GetAsyncKeyState(Win32Interop.VK_RWIN) & 0x8000) != 0;

        if (required.HasFlag(HotkeyModifiers.Control) != ctrl)
        {
            return false;
        }
        if (required.HasFlag(HotkeyModifiers.Alt) != alt)
        {
            return false;
        }
        if (required.HasFlag(HotkeyModifiers.Shift) != shift)
        {
            return false;
        }
        if (required.HasFlag(HotkeyModifiers.Win) != win)
        {
            return false;
        }
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32Interop.WM_USER_REGISTER:
            {
                long packed = wParam.ToInt64();
                targetVirtualKey = (uint)(packed >> 32);
                targetModifiers = (HotkeyModifiers)(uint)(packed & 0xFFFFFFFF);
                return IntPtr.Zero;
            }

            case Win32Interop.WM_USER_UNREGISTER:
                targetVirtualKey = 0;
                targetModifiers = HotkeyModifiers.None;
                return IntPtr.Zero;

            case Win32Interop.WM_USER_QUIT:
                if (hookHandle != IntPtr.Zero)
                {
                    Win32Interop.UnhookWindowsHookEx(hookHandle);
                    hookHandle = IntPtr.Zero;
                }
                Win32Interop.DestroyWindow(hwnd);
                Win32Interop.PostMessage(IntPtr.Zero, Win32Interop.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                return IntPtr.Zero;
        }
        return Win32Interop.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;

        if (windowHandle != IntPtr.Zero)
        {
            Win32Interop.PostMessage(windowHandle, Win32Interop.WM_USER_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        if (thread.IsAlive)
        {
            thread.Join(TimeSpan.FromSeconds(2));
        }

        ready.Dispose();
    }
}
