using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScreenshotManager.Localization;
using ScreenshotManager.Models;
using ScreenshotManager.Services;
using ScreenshotManager.Services.AI;
using Wpf.Ui.Controls;

namespace ScreenshotManager.Views;

/// <summary>
/// Окно настроек приложения.
/// </summary>
public partial class SettingsWindow : FluentWindow
{
    private readonly SettingsService _settingsService;
    private readonly ModelDownloadService _modelDownloadService;
    private readonly BlipInferenceService _blipService;
    private readonly Action _onSettingsChanged;
    private bool _isInitializing = true;

    public SettingsWindow(
        SettingsService settingsService,
        ModelDownloadService modelDownloadService,
        BlipInferenceService blipService,
        Action onSettingsChanged)
    {
        _settingsService = settingsService;
        _modelDownloadService = modelDownloadService;
        _blipService = blipService;
        _onSettingsChanged = onSettingsChanged;

        InitializeComponent();
        LoadSettingsToUI();

        _isInitializing = false;
    }

    /// <summary>
    /// Загрузить текущие настройки в элементы UI.
    /// </summary>
    private void LoadSettingsToUI()
    {
        var s = _settingsService.Settings;

        // Общие
        ToggleEnabled.IsChecked = s.IsEnabled;
        TxtSaveFolder.Text = s.SaveFolder;

        // Формат
        foreach (ComboBoxItem item in CmbFormat.Items)
        {
            if (item.Tag?.ToString() == s.ImageFormat)
            {
                CmbFormat.SelectedItem = item;
                break;
            }
        }

        ToggleAutostart.IsChecked = s.StartWithWindows;

        // Язык
        foreach (ComboBoxItem item in CmbLanguage.Items)
        {
            if (item.Tag?.ToString() == s.Language)
            {
                CmbLanguage.SelectedItem = item;
                break;
            }
        }

        // Именование
        switch (s.NamingMode)
        {
            case NamingMode.DateTime: RbDateTime.IsChecked = true; break;
            case NamingMode.ActiveApp: RbActiveApp.IsChecked = true; break;
            case NamingMode.AI: RbAI.IsChecked = true; break;
            case NamingMode.Combined: RbCombined.IsChecked = true; break;
        }

        // AI
        UpdateAiStatus();
        TxtHfToken.Text = s.HfToken;

        // Хоткеи
        ToggleClipboard.IsChecked = s.MonitorClipboard;
        RefreshHotkeysList();

        // Предпросмотр
        UpdateNamingPreview();
    }

    /// <summary>
    /// Сохранить текущее состояние UI в настройки.
    /// </summary>
    private void SaveAndNotify()
    {
        if (_isInitializing) return;
        _settingsService.Save();
        _onSettingsChanged();
    }

    // ── Event Handlers ────────────────────────────────────────────────

    private void ToggleEnabled_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.Settings.IsEnabled = ToggleEnabled.IsChecked == true;
        SaveAndNotify();
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Loc.Instance["save_folder"],
            SelectedPath = _settingsService.Settings.SaveFolder,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _settingsService.Settings.SaveFolder = dialog.SelectedPath;
            TxtSaveFolder.Text = dialog.SelectedPath;
            SaveAndNotify();
        }
    }

    private void CmbFormat_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (CmbFormat.SelectedItem is ComboBoxItem item && item.Tag is string format)
        {
            _settingsService.Settings.ImageFormat = format;
            SaveAndNotify();
        }
    }

    private void ToggleAutostart_Click(object sender, RoutedEventArgs e)
    {
        var enabled = ToggleAutostart.IsChecked == true;
        _settingsService.Settings.StartWithWindows = enabled;
        SetAutoStart(enabled);
        SaveAndNotify();
    }

    private void CmbLanguage_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (CmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            _settingsService.Settings.Language = lang;
            Loc.Instance.Language = lang;
            SaveAndNotify();
        }
    }

    private void NamingMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var mode = NamingMode.DateTime;
        if (RbActiveApp.IsChecked == true) mode = NamingMode.ActiveApp;
        else if (RbAI.IsChecked == true) mode = NamingMode.AI;
        else if (RbCombined.IsChecked == true) mode = NamingMode.Combined;

        if (mode == NamingMode.AI || mode == NamingMode.Combined)
        {
            if (!_modelDownloadService.IsModelDownloaded())
            {
                TxtAiStatus.Text = Loc.Instance["ai_download_required"];
                // Revert to DateTime
                RbDateTime.IsChecked = true;
                return;
            }

            _settingsService.Settings.AiNamingEnabled = true;
            try
            {
                _blipService.LoadModel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Failed to load model: {ex.Message}");
                _settingsService.Settings.AiNamingEnabled = false;
                TxtAiStatus.Text = $"❌ {ex.Message}";
                RbDateTime.IsChecked = true;
                return;
            }
        }
        else
        {
            _settingsService.Settings.AiNamingEnabled = false;
            _blipService.UnloadModel();
        }

        _settingsService.Settings.NamingMode = mode;
        UpdateNamingPreview();
        SaveAndNotify();
    }

    private async void BtnDownloadAI_Click(object sender, RoutedEventArgs e)
    {
        if (_modelDownloadService.IsModelDownloaded())
        {
            // Удалить модель
            _blipService.UnloadModel();
            _modelDownloadService.DeleteModel();
            _settingsService.Settings.AiModelDownloaded = false;
            _settingsService.Settings.AiNamingEnabled = false;
            UpdateAiStatus();
            
            if (RbAI.IsChecked == true || RbCombined.IsChecked == true)
            {
                RbDateTime.IsChecked = true;
            }
            
            SaveAndNotify();
            return;
        }

        // Скачать модель
        BtnDownloadAI.IsEnabled = false;
        PanelProgress.Visibility = Visibility.Visible;
        TxtAiStatus.Text = Loc.Instance["ai_downloading"];

        _modelDownloadService.ProgressChanged += OnDownloadProgress;
        _modelDownloadService.DownloadCompleted += OnDownloadCompleted;

        await _modelDownloadService.DownloadModelAsync();
    }

    private void OnDownloadProgress(double progress, string description)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressDownload.Value = progress * 100;
            TxtProgressInfo.Text = $"{description}: {progress:P0}";
        });
    }

    private void OnDownloadCompleted(bool success, string? error)
    {
        Dispatcher.Invoke(() =>
        {
            _modelDownloadService.ProgressChanged -= OnDownloadProgress;
            _modelDownloadService.DownloadCompleted -= OnDownloadCompleted;

            PanelProgress.Visibility = Visibility.Collapsed;
            BtnDownloadAI.IsEnabled = true;

            if (success)
            {
                _settingsService.Settings.AiModelDownloaded = true;
                UpdateAiStatus();
                SaveAndNotify();
            }
            else
            {
                TxtAiStatus.Text = $"❌ {error}";
            }
        });
    }

    private void TxtHfToken_TextChanged(object sender, TextChangedEventArgs e)
    {
        _settingsService.Settings.HfToken = TxtHfToken.Text;
        SaveAndNotify();
    }

    private void Hyperlink_HfToken_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://huggingface.co/settings/tokens",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Settings] Failed to open link: {ex.Message}");
        }
    }

    private void ToggleClipboard_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.Settings.MonitorClipboard = ToggleClipboard.IsChecked == true;
        SaveAndNotify();
    }

    private void BtnAddHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyRecorder.RecordedModifiers == 0 && HotkeyRecorder.RecordedKey == 0)
            return;

        var binding = new HotkeyBinding
        {
            Id = _settingsService.Settings.NextHotkeyId++,
            Modifiers = HotkeyRecorder.RecordedModifiers,
            VirtualKey = HotkeyRecorder.RecordedKey,
            DisplayName = HotkeyRecorder.DisplayText,
            IsEnabled = true
        };

        _settingsService.Settings.CustomHotkeys.Add(binding);
        HotkeyRecorder.Reset();
        RefreshHotkeysList();
        SaveAndNotify();
    }

    private void HotkeyRecorder_HotkeyRecorded(object sender, EventArgs e)
    {
        // Можно добавить визуальную индикацию
    }

    private void BtnRemoveHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is int id)
        {
            _settingsService.Settings.CustomHotkeys.RemoveAll(h => h.Id == id);
            RefreshHotkeysList();
            SaveAndNotify();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void UpdateAiStatus()
    {
        if (_modelDownloadService.IsModelDownloaded())
        {
            TxtAiStatus.Text = Loc.Instance["ai_downloaded"];
            BtnDownloadAI.Content = Loc.Instance["ai_delete_btn"];
        }
        else
        {
            TxtAiStatus.Text = Loc.Instance["ai_not_downloaded"];
            BtnDownloadAI.Content = Loc.Instance["ai_download_btn"];
        }
    }

    private void UpdateNamingPreview()
    {
        var now = DateTime.Now;
        var preview = _settingsService.Settings.NamingMode switch
        {
            NamingMode.DateTime => $"Screenshot_{now:yyyy-MM-dd}_{now:HH-mm-ss}.png",
            NamingMode.ActiveApp => $"Chrome_{now:yyyy-MM-dd}_{now:HH-mm-ss}.png",
            NamingMode.AI => $"code_editor_with_dark_theme_{now:yyyy-MM-dd}.png",
            NamingMode.Combined => $"VSCode_code_editor_dark_theme_{now:yyyy-MM-dd}_{now:HH-mm}.png",
            _ => $"Screenshot_{now:yyyy-MM-dd}_{now:HH-mm-ss}.png"
        };

        TxtNamingPreview.Text = preview;
    }

    private void RefreshHotkeysList()
    {
        HotkeysList.ItemsSource = null;
        HotkeysList.ItemsSource = _settingsService.Settings.CustomHotkeys;
    }

    /// <summary>
    /// Установить/снять автозапуск через реестр.
    /// </summary>
    private static void SetAutoStart(bool enable)
    {
        try
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            const string appName = "ScreenshotManager";

            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null)
                    key.SetValue(appName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(appName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Settings] AutoStart error: {ex.Message}");
        }
    }

    /// <summary>
    /// При закрытии — скрыть окно вместо уничтожения.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
