using System.Diagnostics;
using System.Runtime.InteropServices;
using ScreenshotManager.Helpers;
using ScreenshotManager.Models;

namespace ScreenshotManager.Services;

/// <summary>
/// Регистрация и управление глобальными хоткеями через Low-Level Keyboard Hook.
/// Это позволяет перехватывать PrintScreen и блокировать его от Windows (чтобы не было двойного скриншота).
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeInterop.LowLevelKeyboardProc _proc;
    private readonly Dictionary<int, HotkeyBinding> _registeredHotkeys = new();
    private bool _disposed;

    /// <summary>
    /// Вызывается при нажатии зарегистрированного хоткея.
    /// </summary>
    public event Action<HotkeyBinding>? HotkeyPressed;

    public GlobalHotkeyService()
    {
        _proc = HookCallback;
    }

    /// <summary>
    /// Инициализировать сервис.
    /// </summary>
    public void Initialize()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        if (curModule?.ModuleName != null)
        {
            _hookId = NativeInterop.SetWindowsHookEx(
                NativeInterop.WH_KEYBOARD_LL,
                _proc,
                NativeInterop.GetModuleHandle(curModule.ModuleName),
                0);
        }

        Debug.WriteLine("[GlobalHotkey] Initialized LL Hook");
    }

    /// <summary>
    /// Зарегистрировать хоткей.
    /// </summary>
    public bool RegisterHotkey(HotkeyBinding binding)
    {
        _registeredHotkeys[binding.Id] = binding;
        Debug.WriteLine($"[GlobalHotkey] Registered: {binding.DisplayName} (ID={binding.Id})");
        return true;
    }

    /// <summary>
    /// Снять регистрацию хоткея.
    /// </summary>
    public void UnregisterHotkey(int id)
    {
        _registeredHotkeys.Remove(id);
        Debug.WriteLine($"[GlobalHotkey] Unregistered: ID={id}");
    }

    /// <summary>
    /// Снять регистрацию всех хоткеев.
    /// </summary>
    public void UnregisterAll()
    {
        _registeredHotkeys.Clear();
    }

    /// <summary>
    /// Зарегистрировать все хоткеи из списка настроек.
    /// </summary>
    public void RegisterFromSettings(List<HotkeyBinding> hotkeys)
    {
        UnregisterAll();
        foreach (var hk in hotkeys.Where(h => h.IsEnabled))
        {
            RegisterHotkey(hk);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)NativeInterop.WM_KEYDOWN || wParam == (IntPtr)NativeInterop.WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);
            uint currentModifiers = GetCurrentModifiers();

            foreach (var binding in _registeredHotkeys.Values)
            {
                // Для RegisterHotKey модификаторы включают флаг NoRepeat (0x4000), уберем его для сравнения
                uint bindingMods = binding.Modifiers & ~(uint)HotkeyModifiers.NoRepeat;
                
                if (binding.VirtualKey == vkCode && bindingMods == currentModifiers)
                {
                    Debug.WriteLine($"[GlobalHotkey] Pressed: {binding.DisplayName}");
                    
                    // Запускаем событие асинхронно, чтобы не блокировать хук
                    Task.Run(() => HotkeyPressed?.Invoke(binding));
                    
                    // Блокируем дальнейшую обработку этого нажатия системой (останавливает встроенный скриншотер Windows)
                    return (IntPtr)1;
                }
            }
        }

        return NativeInterop.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private uint GetCurrentModifiers()
    {
        uint modifiers = (uint)HotkeyModifiers.None;

        if (IsKeyPressed(NativeInterop.VK_MENU)) modifiers |= (uint)HotkeyModifiers.Alt;
        if (IsKeyPressed(NativeInterop.VK_CONTROL)) modifiers |= (uint)HotkeyModifiers.Ctrl;
        if (IsKeyPressed(NativeInterop.VK_SHIFT)) modifiers |= (uint)HotkeyModifiers.Shift;
        if (IsKeyPressed(NativeInterop.VK_LWIN) || IsKeyPressed(NativeInterop.VK_RWIN)) modifiers |= (uint)HotkeyModifiers.Win;

        return modifiers;
    }

    private bool IsKeyPressed(int vKey)
    {
        return (NativeInterop.GetAsyncKeyState(vKey) & 0x8000) != 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();
        if (_hookId != IntPtr.Zero)
        {
            NativeInterop.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
