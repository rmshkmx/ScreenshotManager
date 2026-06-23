using System.Diagnostics;
using System.Drawing;
using ScreenshotManager.Localization;
using ScreenshotManager.Models;
using ScreenshotManager.Naming;
using ScreenshotManager.Services.AI;

namespace ScreenshotManager.Services;

/// <summary>
/// Центральный координатор обработки скриншотов.
/// Объединяет clipboard monitor, hotkey service, naming strategies и saver.
/// Реализует защиту от дублирования (hotkey → clipboard race condition).
/// </summary>
public sealed class ScreenshotProcessorService
{
    private readonly SettingsService _settingsService;
    private readonly ActiveWindowService _activeWindowService;
    private readonly ScreenshotSaverService _saverService;
    private readonly ScreenCaptureService _captureService;
    private readonly BlipInferenceService _blipService;

    // Защита от дублирования: флаги и таймстемпы
    private DateTime _lastHotkeyCapture = DateTime.MinValue;
    private DateTime _lastClipboardCapture = DateTime.MinValue;
    private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ClipboardDeduplicationWindow = TimeSpan.FromMilliseconds(500);
    private readonly object _lock = new();

    // Naming strategies
    private readonly DateTimeNamer _dateTimeNamer = new();
    private readonly ActiveWindowNamer _activeWindowNamer = new();
    private AINamer? _aiNamer;
    private CombinedNamer? _combinedNamer;

    /// <summary>
    /// Событие: скриншот успешно сохранён (путь к файлу).
    /// </summary>
    public event Action<string>? ScreenshotSaved;

    /// <summary>
    /// Событие: ошибка при обработке скриншота.
    /// </summary>
    public event Action<string>? ProcessingError;

    public ScreenshotProcessorService(
        SettingsService settingsService,
        ActiveWindowService activeWindowService,
        ScreenshotSaverService saverService,
        ScreenCaptureService captureService,
        BlipInferenceService blipService)
    {
        _settingsService = settingsService;
        _activeWindowService = activeWindowService;
        _saverService = saverService;
        _captureService = captureService;
        _blipService = blipService;
    }

    /// <summary>
    /// Обработать скриншот из буфера обмена.
    /// </summary>
    public async Task ProcessClipboardScreenshotAsync(Bitmap screenshot)
    {
        // Проверка: утилита включена?
        if (!_settingsService.Settings.IsEnabled)
            return;

        // Защита от дублирования: если недавно был хоткей-захват или другое событие буфера — пропускаем
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            if (now - _lastHotkeyCapture < DeduplicationWindow)
            {
                Debug.WriteLine("[Processor] Skipping clipboard event (hotkey deduplication)");
                screenshot.Dispose();
                return;
            }

            if (now - _lastClipboardCapture < ClipboardDeduplicationWindow)
            {
                Debug.WriteLine("[Processor] Skipping clipboard event (clipboard consecutive deduplication)");
                screenshot.Dispose();
                return;
            }

            _lastClipboardCapture = now;
        }

        await ProcessScreenshotInternalAsync(screenshot);
    }

    /// <summary>
    /// Обработать скриншот по нажатию хоткея.
    /// </summary>
    public async Task ProcessHotkeyScreenshotAsync()
    {
        // Проверка: утилита включена?
        if (!_settingsService.Settings.IsEnabled)
            return;

        // Установить флаг-блокиратор ДО захвата
        lock (_lock)
        {
            _lastHotkeyCapture = DateTime.UtcNow;
        }

        try
        {
            var screenshot = _captureService.CapturePrimaryScreen();
            await ProcessScreenshotInternalAsync(screenshot);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Processor] Hotkey capture error: {ex.Message}");
            ProcessingError?.Invoke(ex.Message);
        }
    }

    /// <summary>
    /// Внутренняя обработка скриншота: именование + сохранение.
    /// </summary>
    private async Task ProcessScreenshotInternalAsync(Bitmap screenshot)
    {
        try
        {
            var (title, processName) = _activeWindowService.GetActiveWindowInfo();
            var strategy = GetCurrentStrategy();
            var fileName = await strategy.GenerateNameAsync(screenshot, title, processName);
            var savedPath = _saverService.Save(screenshot, fileName);

            Debug.WriteLine($"[Processor] Saved: {savedPath}");
            ScreenshotSaved?.Invoke(savedPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Processor] Error: {ex.Message}");
            ProcessingError?.Invoke(ex.Message);
        }
        finally
        {
            screenshot.Dispose();
        }
    }

    /// <summary>
    /// Получить текущую стратегию именования.
    /// </summary>
    private INamingStrategy GetCurrentStrategy()
    {
        var mode = _settingsService.Settings.NamingMode;

        // Если выбран AI-режим, но модель не загружена — фолбэк на DateTime
        if ((mode == NamingMode.AI || mode == NamingMode.Combined) &&
            !_settingsService.Settings.AiNamingEnabled)
        {
            mode = NamingMode.DateTime;
        }

        return mode switch
        {
            NamingMode.ActiveApp => _activeWindowNamer,
            NamingMode.AI => GetAINamer(),
            NamingMode.Combined => GetCombinedNamer(),
            _ => _dateTimeNamer
        };
    }

    private AINamer GetAINamer()
    {
        _aiNamer ??= new AINamer(_blipService);
        return _aiNamer;
    }

    private CombinedNamer GetCombinedNamer()
    {
        _combinedNamer ??= new CombinedNamer(_blipService);
        return _combinedNamer;
    }
}
