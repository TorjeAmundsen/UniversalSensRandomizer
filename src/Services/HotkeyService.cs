using System;
using UniversalSensRandomizer.Interop;
using UniversalSensRandomizer.Models;

namespace UniversalSensRandomizer.Services;

public sealed class HotkeyService : IDisposable
{
    private readonly MessageOnlyWindow window;
    private HotkeyCombination current = HotkeyCombination.None;
    private bool disposed;

    public event Action? HotkeyPressed;

    public HotkeyService()
    {
        window = new MessageOnlyWindow();
        window.HotkeyPressed += OnHotkeyPressed;
    }

    public HotkeyCombination Current => current;

    public void Register(HotkeyCombination combo)
    {
        current = combo;
        window.RequestRegister(combo);
    }

    public void Unregister()
    {
        current = HotkeyCombination.None;
        window.RequestUnregister();
    }

    private void OnHotkeyPressed()
    {
        HotkeyPressed?.Invoke();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        window.HotkeyPressed -= OnHotkeyPressed;
        window.Dispose();
    }
}
