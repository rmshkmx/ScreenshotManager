using System.Diagnostics;
using System.Windows.Interop;
using ScreenshotManager.Helpers;
using ScreenshotManager.Models;

namespace ScreenshotManager.Services;

/// <summary>
/// Регистрация и управление глобальными хоткеями через RegisterHotKey.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private HwndSource? _hwndSource;
    private readonly Dictionary<int, HotkeyBinding> _registeredHotkeys = new();
    private bool _disposed;

    /// <summary>
    /// Вызывается при нажатии зарегистрированного хоткея.
    /// </summary>
    public event Action<HotkeyBinding>? HotkeyPressed;

    /// <summary>
    /// Инициализировать сервис. Должен вызываться из UI-потока.
    /// </summary>
    public void Initialize()
    {
        var parameters = new HwndSourceParameters("ScreenshotManager_HotkeyHost")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        Debug.WriteLine("[GlobalHotkey] Initialized");
    }

    /// <summary>
    /// Зарегистрировать хоткей.
    /// </summary>
    /// <returns>true если регистрация прошла успешно.</returns>
    public bool RegisterHotkey(HotkeyBinding binding)
    {
        if (_hwndSource == null)
            throw new InvalidOperationException("Service not initialized. Call Initialize() first.");

        if (_registeredHotkeys.ContainsKey(binding.Id))
        {
            UnregisterHotkey(binding.Id);
        }

        bool result = NativeInterop.RegisterHotKey(
            _hwndSource.Handle,
            binding.Id,
            binding.Modifiers | (uint)HotkeyModifiers.NoRepeat,
            binding.VirtualKey);

        if (result)
        {
            _registeredHotkeys[binding.Id] = binding;
            Debug.WriteLine($"[GlobalHotkey] Registered: {binding.DisplayName} (ID={binding.Id})");
        }
        else
        {
            Debug.WriteLine($"[GlobalHotkey] Failed to register: {binding.DisplayName}");
        }

        return result;
    }

    /// <summary>
    /// Снять регистрацию хоткея.
    /// </summary>
    public void UnregisterHotkey(int id)
    {
        if (_hwndSource == null) return;

        NativeInterop.UnregisterHotKey(_hwndSource.Handle, id);
        _registeredHotkeys.Remove(id);
        Debug.WriteLine($"[GlobalHotkey] Unregistered: ID={id}");
    }

    /// <summary>
    /// Снять регистрацию всех хоткеев.
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in _registeredHotkeys.Keys.ToList())
        {
            UnregisterHotkey(id);
        }
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

    /// <summary>
    /// Обработчик оконных сообщений.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeInterop.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_registeredHotkeys.TryGetValue(id, out var binding))
            {
                Debug.WriteLine($"[GlobalHotkey] Pressed: {binding.DisplayName}");
                HotkeyPressed?.Invoke(binding);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
    }
}
