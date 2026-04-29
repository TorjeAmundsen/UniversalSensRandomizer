using System;
using System.Globalization;
using System.Text;

namespace UniversalSensRandomizer.Models;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

public readonly record struct HotkeyCombination(uint VirtualKey, HotkeyModifiers Modifiers)
{
    public static HotkeyCombination None { get; } = new(0, HotkeyModifiers.None);

    public bool IsEmpty => VirtualKey == 0;

    public string ToDisplayString()
    {
        if (IsEmpty)
        {
            return "None";
        }

        StringBuilder builder = new();
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            builder.Append("Ctrl+");
        }
        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            builder.Append("Alt+");
        }
        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            builder.Append("Shift+");
        }
        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            builder.Append("Win+");
        }

        builder.Append(KeyLabel(VirtualKey));
        return builder.ToString();
    }

    private static string KeyLabel(uint vk)
    {
        // F1..F24 = 0x70..0x87
        if (vk is >= 0x70 and <= 0x87)
        {
            return "F" + (vk - 0x70 + 1).ToString(CultureInfo.InvariantCulture);
        }
        // 0..9 + A..Z map directly
        if (vk is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
        {
            return ((char)vk).ToString();
        }
        return vk switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x13 => "Pause",
            0x14 => "CapsLock",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2C => "PrintScreen",
            0x2D => "Insert",
            0x2E => "Delete",
            _ => "Key 0x" + vk.ToString("X2", CultureInfo.InvariantCulture),
        };
    }
}
