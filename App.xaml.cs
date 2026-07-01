using System.Diagnostics;
using System.Drawing;
using System.Windows;
using ScreenshotManager.Localization;
using ScreenshotManager.Models;
using ScreenshotManager.Services;
using ScreenshotManager.Services.AI;
using ScreenshotManager.Views;
using WinForms = System.Windows.Forms;
using Application = System.Windows.Application;

namespace ScreenshotManager;

/// <summary>
/// Точка входа приложения. Управляет жизненным циклом:
/// трей → сервисы → окно настроек.
/// </summary>
public partial class App : Application
{
    // ── Services ──────────────────────────────────────────────────────

    private readonly SettingsService _settingsService = new();
    private readonly ActiveWindowService _activeWindowService = new();
    private readonly ScreenCaptureService _captureService = new();
    private readonly BlipInferenceService _blipService = new();
    private ModelDownloadService _modelDownloadService = null!;

    private ScreenshotSaverService _saverService = null!;
    private ScreenshotProcessorService _processorService = null!;
    private ClipboardMonitorService _clipboardMonitor = null!;
    private GlobalHotkeyService _hotkeyService = null!;

    // ── UI ────────────────────────────────────────────────────────────

    private WinForms.NotifyIcon _trayIcon = null!;
    private WinForms.ToolStripMenuItem _toggleMenuItem = null!;
    private SettingsWindow? _settingsWindow;

    // ── Lifecycle ─────────────────────────────────────────────────────

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Загрузить настройки
        _settingsService.Load();

        // Инициализировать сервисы
        _modelDownloadService = new ModelDownloadService(_settingsService);
        _saverService = new ScreenshotSaverService(_settingsService);
        _processorService = new ScreenshotProcessorService(
            _settingsService, _activeWindowService, _saverService, _captureService, _blipService);

        _clipboardMonitor = new ClipboardMonitorService();
        _hotkeyService = new GlobalHotkeyService();

        // Подписки на события
        _clipboardMonitor.ClipboardImageCaptured += OnClipboardImage;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _processorService.ScreenshotSaved += OnScreenshotSaved;
        _processorService.ProcessingError += OnProcessingError;

        // Создать трей
        CreateTrayIcon();

        // Запустить сервисы
        _hotkeyService.Initialize();
        ApplySettings();

        // Показать уведомление о запуске в трее
        _trayIcon.ShowBalloonTip(
            3000,
            Loc.Instance["notify_started"],
            Loc.Instance["notify_started_text"],
            WinForms.ToolTipIcon.Info);

        Debug.WriteLine("[App] Started successfully");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Корректная очистка
        _clipboardMonitor.Dispose();
        _hotkeyService.Dispose();
        _blipService.Dispose();
        _trayIcon.Dispose();

        base.OnExit(e);
    }

    // ── Tray Icon ─────────────────────────────────────────────────────

    private void CreateTrayIcon()
    {
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = CreateAppIcon(),
            Text = Loc.Instance["tray_tooltip"],
            Visible = true
        };

        // Контекстное меню трея
        var menu = new WinForms.ContextMenuStrip();

        _toggleMenuItem = new WinForms.ToolStripMenuItem(
            _settingsService.Settings.IsEnabled ? Loc.Instance["tray_enabled"] : Loc.Instance["tray_disabled"]);
        _toggleMenuItem.Click += (_, _) => ToggleEnabled();
        _toggleMenuItem.Font = new Font(_toggleMenuItem.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add(_toggleMenuItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        var settingsItem = new WinForms.ToolStripMenuItem(Loc.Instance["tray_settings"]);
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        var folderItem = new WinForms.ToolStripMenuItem(Loc.Instance["tray_open_folder"]);
        folderItem.Click += (_, _) => OpenSaveFolder();
        menu.Items.Add(folderItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        var exitItem = new WinForms.ToolStripMenuItem(Loc.Instance["tray_exit"]);
        exitItem.Click += (_, _) => ExitApp();
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowSettings();
    }

    /// <summary>
    /// Создать иконку для трея программно (камера).
    /// </summary>
    private static System.Drawing.Icon CreateAppIcon()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Фон — скруглённый прямоугольник (тёмно-синий)
        using var bgBrush = new SolidBrush(Color.FromArgb(60, 120, 216));
        g.FillEllipse(bgBrush, 1, 1, 30, 30);

        // Тело камеры
        using var cameraBrush = new SolidBrush(Color.White);
        g.FillRectangle(cameraBrush, 7, 11, 18, 13);

        // Объектив камеры
        using var lensBrush = new SolidBrush(Color.FromArgb(60, 120, 216));
        g.FillEllipse(lensBrush, 12, 13, 8, 8);

        // Вспышка
        g.FillRectangle(cameraBrush, 11, 8, 6, 4);

        var handle = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(handle);
    }

    // ── Actions ───────────────────────────────────────────────────────

    private void ToggleEnabled()
    {
        _settingsService.Settings.IsEnabled = !_settingsService.Settings.IsEnabled;
        _settingsService.Save();
        UpdateTrayStatus();
    }

    private void ShowSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(
                _settingsService,
                _modelDownloadService,
                _blipService,
                OnSettingsChanged);
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
        if (_settingsWindow.WindowState == WindowState.Minimized)
            _settingsWindow.WindowState = WindowState.Normal;
    }

    private void OpenSaveFolder()
    {
        var folder = _settingsService.Settings.SaveFolder;
        if (Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        Shutdown();
    }

    // ── Event Handlers ────────────────────────────────────────────────

    private async void OnClipboardImage(Bitmap bitmap)
    {
        await _processorService.ProcessClipboardScreenshotAsync(bitmap);
    }

    private async void OnHotkeyPressed(HotkeyBinding binding)
    {
        await _processorService.ProcessHotkeyScreenshotAsync();
    }

    private void OnScreenshotSaved(string path)
    {
        Dispatcher.Invoke(() =>
        {
            var fileName = Path.GetFileName(path);
            _trayIcon.ShowBalloonTip(
                2000,
                Loc.Instance["notify_saved"],
                fileName,
                WinForms.ToolTipIcon.Info);
        });
    }

    private void OnProcessingError(string error)
    {
        Dispatcher.Invoke(() =>
        {
            _trayIcon.ShowBalloonTip(
                3000,
                Loc.Instance["notify_error"],
                error,
                WinForms.ToolTipIcon.Error);
        });
    }

    // ── Settings Sync ─────────────────────────────────────────────────

    private void OnSettingsChanged()
    {
        Dispatcher.Invoke(ApplySettings);
    }

    private void ApplySettings()
    {
        var s = _settingsService.Settings;

        // Clipboard monitor
        if (s.MonitorClipboard && s.IsEnabled)
            _clipboardMonitor.Start();
        else
            _clipboardMonitor.Stop();

        // Hotkeys
        _hotkeyService.RegisterFromSettings(s.CustomHotkeys);

        // Tray status
        UpdateTrayStatus();
    }

    private void UpdateTrayStatus()
    {
        var enabled = _settingsService.Settings.IsEnabled;
        _toggleMenuItem.Text = enabled ? Loc.Instance["tray_enabled"] : Loc.Instance["tray_disabled"];
        _trayIcon.Text = $"ScreenshotManager - {(enabled ? Loc.Instance["tray_toggle_off"] : Loc.Instance["tray_toggle_on"])}";
    }
}
