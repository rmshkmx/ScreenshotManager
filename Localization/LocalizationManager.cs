using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenshotManager.Localization;

/// <summary>
/// Менеджер локализации с поддержкой привязки WPF.
/// Использование в XAML: {Binding [key], Source={x:Static local:Loc.Instance}}
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private string _language = "ru";

    public string Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            // Уведомляем WPF об изменении всех привязок к индексатору
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }

    /// <summary>
    /// Индексатор для получения локализованной строки по ключу.
    /// </summary>
    public string this[string key] =>
        _resources.TryGetValue(_language, out var dict) && dict.TryGetValue(key, out var value)
            ? value
            : key;

    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Ресурсы ───────────────────────────────────────────────────────

    private readonly Dictionary<string, Dictionary<string, string>> _resources = new()
    {
        ["ru"] = new()
        {
            // Трей
            ["tray_tooltip"] = "ScreenshotManager",
            ["tray_enabled"] = "✅ Включено",
            ["tray_disabled"] = "⏸ Выключено",
            ["tray_toggle_on"] = "Включить",
            ["tray_toggle_off"] = "Выключить",
            ["tray_settings"] = "Настройки",
            ["tray_open_folder"] = "Открыть папку",
            ["tray_exit"] = "Выход",

            // Настройки — заголовки
            ["settings_title"] = "ScreenshotManager — Настройки",
            ["settings_general"] = "Общие настройки",
            ["settings_naming"] = "Именование файлов",
            ["settings_ai"] = "AI-компоненты",
            ["settings_hotkeys"] = "Горячие клавиши",

            // Общие
            ["enabled"] = "Включено",
            ["save_folder"] = "Папка сохранения",
            ["browse"] = "Обзор...",
            ["image_format"] = "Формат изображения",
            ["start_with_windows"] = "Запуск с Windows",
            ["language"] = "Язык / Language",

            // Именование
            ["naming_mode"] = "Режим именования",
            ["naming_datetime"] = "По дате и времени",
            ["naming_activeapp"] = "По активному приложению",
            ["naming_ai"] = "AI (BLIP)",
            ["naming_combined"] = "Комбинация",
            ["naming_preview"] = "Предпросмотр:",

            // AI
            ["ai_status"] = "Статус модели",
            ["ai_not_downloaded"] = "❌ Не скачана",
            ["ai_downloaded"] = "✅ Скачана и готова",
            ["ai_downloading"] = "⏳ Загрузка...",
            ["ai_download_btn"] = "Скачать модель BLIP",
            ["ai_delete_btn"] = "Удалить модель",
            ["ai_naming_toggle"] = "AI-именование",
            ["ai_download_required"] = "Сначала скачайте модель",

            // Хоткеи
            ["clipboard_monitor"] = "Мониторинг буфера обмена",
            ["clipboard_hint"] = "Автоматически сохраняет изображения из буфера (PrintScreen, Win+Shift+S)",
            ["custom_hotkeys"] = "Пользовательские хоткеи",
            ["add_hotkey"] = "+ Добавить хоткей",
            ["remove_hotkey"] = "Удалить",
            ["press_keys"] = "Нажмите комбинацию клавиш...",
            ["record"] = "Записать",
            ["stop_record"] = "Готово",

            // Уведомления
            ["notify_saved"] = "Скриншот сохранён",
            ["notify_error"] = "Ошибка сохранения",
            ["notify_ai_ready"] = "AI-модель загружена и готова",
            ["notify_ai_error"] = "Ошибка загрузки AI-модели",
        },

        ["en"] = new()
        {
            // Tray
            ["tray_tooltip"] = "ScreenshotManager",
            ["tray_enabled"] = "✅ Enabled",
            ["tray_disabled"] = "⏸ Disabled",
            ["tray_toggle_on"] = "Enable",
            ["tray_toggle_off"] = "Disable",
            ["tray_settings"] = "Settings",
            ["tray_open_folder"] = "Open folder",
            ["tray_exit"] = "Exit",

            // Settings — headers
            ["settings_title"] = "ScreenshotManager — Settings",
            ["settings_general"] = "General Settings",
            ["settings_naming"] = "File Naming",
            ["settings_ai"] = "AI Components",
            ["settings_hotkeys"] = "Hotkeys",

            // General
            ["enabled"] = "Enabled",
            ["save_folder"] = "Save folder",
            ["browse"] = "Browse...",
            ["image_format"] = "Image format",
            ["start_with_windows"] = "Start with Windows",
            ["language"] = "Language / Язык",

            // Naming
            ["naming_mode"] = "Naming mode",
            ["naming_datetime"] = "Date and time",
            ["naming_activeapp"] = "Active application",
            ["naming_ai"] = "AI (BLIP)",
            ["naming_combined"] = "Combined",
            ["naming_preview"] = "Preview:",

            // AI
            ["ai_status"] = "Model status",
            ["ai_not_downloaded"] = "❌ Not downloaded",
            ["ai_downloaded"] = "✅ Downloaded and ready",
            ["ai_downloading"] = "⏳ Downloading...",
            ["ai_download_btn"] = "Download BLIP model",
            ["ai_delete_btn"] = "Delete model",
            ["ai_naming_toggle"] = "AI naming",
            ["ai_download_required"] = "Download the model first",

            // Hotkeys
            ["clipboard_monitor"] = "Clipboard monitoring",
            ["clipboard_hint"] = "Automatically saves images from clipboard (PrintScreen, Win+Shift+S)",
            ["custom_hotkeys"] = "Custom hotkeys",
            ["add_hotkey"] = "+ Add hotkey",
            ["remove_hotkey"] = "Remove",
            ["press_keys"] = "Press key combination...",
            ["record"] = "Record",
            ["stop_record"] = "Done",

            // Notifications
            ["notify_saved"] = "Screenshot saved",
            ["notify_error"] = "Save error",
            ["notify_ai_ready"] = "AI model loaded and ready",
            ["notify_ai_error"] = "AI model download error",
        }
    };
}
