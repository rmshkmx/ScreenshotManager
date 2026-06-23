using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenshotManager.Localization;
using ScreenshotManager.Models;
using UserControl = System.Windows.Controls.UserControl;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ScreenshotManager.Views.Controls;

/// <summary>
/// Контрол для записи комбинации горячих клавиш.
/// Нажми Record → нажми комбинацию → комбинация отображается.
/// </summary>
public partial class HotkeyRecorderControl : UserControl
{
    private bool _isRecording;

    /// <summary>Записанные модификаторы (для RegisterHotKey API).</summary>
    public uint RecordedModifiers { get; private set; }

    /// <summary>Записанный виртуальный код клавиши.</summary>
    public uint RecordedKey { get; private set; }

    /// <summary>Отображаемый текст комбинации.</summary>
    public string DisplayText => TxtHotkey.Text;

    /// <summary>Вызывается после успешной записи хоткея.</summary>
    public event EventHandler? HotkeyRecorded;

    public HotkeyRecorderControl()
    {
        InitializeComponent();
    }

    private void BtnRecord_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        _isRecording = true;
        RecordedModifiers = 0;
        RecordedKey = 0;

        BtnRecord.Content = Loc.Instance["stop_record"];
        TxtHotkey.Text = Loc.Instance["press_keys"];
        TxtHotkey.Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorPrimaryBrush");

        // Перехватываем клавиатуру на уровне окна
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown += Window_PreviewKeyDown;
        }

        Focus();
    }

    private void StopRecording()
    {
        _isRecording = false;
        BtnRecord.Content = Loc.Instance["record"];

        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown -= Window_PreviewKeyDown;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Игнорировать только модификаторы
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            // Обновить отображение текущих модификаторов
            UpdateModifierDisplay();
            return;
        }

        // Собрать модификаторы
        uint modifiers = 0;
        var parts = new List<string>();

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            modifiers |= (uint)HotkeyModifiers.Ctrl;
            parts.Add("Ctrl");
        }
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
        {
            modifiers |= (uint)HotkeyModifiers.Alt;
            parts.Add("Alt");
        }
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            modifiers |= (uint)HotkeyModifiers.Shift;
            parts.Add("Shift");
        }
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
        {
            modifiers |= (uint)HotkeyModifiers.Win;
            parts.Add("Win");
        }

        // Нужен хотя бы один модификатор
        if (modifiers == 0)
        {
            TxtHotkey.Text = "⚠ Нужен модификатор (Ctrl/Alt/Shift)";
            return;
        }

        // Конвертировать WPF Key → WinAPI Virtual Key
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

        RecordedModifiers = modifiers;
        RecordedKey = virtualKey;

        parts.Add(key.ToString());
        TxtHotkey.Text = string.Join(" + ", parts);

        StopRecording();
        HotkeyRecorded?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateModifierDisplay()
    {
        var parts = new List<string>();

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            parts.Add("Ctrl");
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            parts.Add("Alt");
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            parts.Add("Shift");
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            parts.Add("Win");

        if (parts.Count > 0)
            TxtHotkey.Text = string.Join(" + ", parts) + " + ...";
    }

    /// <summary>
    /// Сбросить контрол в начальное состояние.
    /// </summary>
    public void Reset()
    {
        RecordedModifiers = 0;
        RecordedKey = 0;
        TxtHotkey.Text = Loc.Instance["press_keys"];
        TxtHotkey.Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush");
        StopRecording();
    }
}
