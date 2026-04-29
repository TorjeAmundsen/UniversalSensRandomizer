using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using UniversalSensRandomizer.Models;
using UniversalSensRandomizer.ViewModels;

namespace UniversalSensRandomizer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSpinIncrement(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && this.FindControl<NumericUpDown>(name) is { } nud)
        {
            nud.Value = (nud.Value ?? 0) + nud.Increment;
        }
    }

    private void OnSpinDecrement(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && this.FindControl<NumericUpDown>(name) is { } nud)
        {
            nud.Value = (nud.Value ?? 0) - nud.Increment;
        }
    }

    private void OnCaptureHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }
        if (!vm.IsCapturingHotkey)
        {
            return;
        }

        Key key = e.Key;
        if (IsPureModifier(key))
        {
            return;
        }
        if (key == Key.Escape)
        {
            vm.CancelCapture();
            e.Handled = true;
            return;
        }

        uint virtualKey = AvaloniaKeyToVirtualKey(key);
        if (virtualKey == 0)
        {
            return;
        }

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            modifiers |= HotkeyModifiers.Win;
        }

        vm.ApplyCapturedHotkey(virtualKey, modifiers);
        e.Handled = true;
    }

    private void OnCaptureHotkeyLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.IsCapturingHotkey)
        {
            vm.CancelCapture();
        }
    }

    private static bool IsPureModifier(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin
            or Key.System;
    }

    private static uint AvaloniaKeyToVirtualKey(Key key)
    {
        // Avalonia's Key enum mostly matches Win32 VKs; specific mappings below.
        if (key is >= Key.A and <= Key.Z)
        {
            return (uint)('A' + (key - Key.A));
        }
        if (key is >= Key.D0 and <= Key.D9)
        {
            return (uint)('0' + (key - Key.D0));
        }
        if (key is >= Key.F1 and <= Key.F24)
        {
            return (uint)(0x70 + (key - Key.F1));
        }
        return key switch
        {
            Key.Space => 0x20,
            Key.Enter => 0x0D,
            Key.Tab => 0x09,
            Key.Back => 0x08,
            Key.Escape => 0x1B,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            Key.Home => 0x24,
            Key.End => 0x23,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.Up => 0x26,
            Key.Down => 0x28,
            Key.Left => 0x25,
            Key.Right => 0x27,
            _ => 0,
        };
    }
}
