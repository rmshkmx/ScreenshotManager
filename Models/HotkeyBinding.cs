using System.Text.Json.Serialization;
using System.Windows.Input;

namespace ScreenshotManager.Models;

/// <summary>
/// Пользовательская привязка горячей клавиши для захвата скриншота.
/// </summary>
public class HotkeyBinding
{
    /// <summary>Уникальный ID привязки (используется для RegisterHotKey).</summary>
    public int Id { get; set; }

    /// <summary>Модификаторы (Ctrl, Alt, Shift, Win).</summary>
    public uint Modifiers { get; set; }

    /// <summary>Виртуальный код клавиши.</summary>
    public uint VirtualKey { get; set; }

    /// <summary>Отображаемое имя комбинации, например "Ctrl+Shift+S".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Активна ли данная привязка.</summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Модификаторы для RegisterHotKey API.
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0x0000,
    Alt = 0x0001,
    Ctrl = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
    NoRepeat = 0x4000
}
