using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Cloris.Aion2Flow.Services.Audio;
using Cloris.Aion2Flow.Services.Logging;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// Makes a boss alert sound from any sound or video file: it asks for the file first, then shows its waveform to cut the
// alert from (at most 10 s), plays the edges as they're let go, and saves it as the newest of the user's sounds.
public partial class AlertSoundEditorWindow : Window
{
    // What a let-go edge plays: this much after the start, or up to the end.
    private const double EdgePreviewSeconds = 1.5;
    private static readonly string[] SoundFilePatterns =
        ["*.mp3", "*.m4a", "*.aac", "*.wav", "*.wma", "*.flac", "*.ogg", "*.opus", "*.mp4", "*.m4v", "*.mov", "*.mkv", "*.webm", "*.wmv", "*.avi"];
    private static AlertSoundEditorWindow? _open;
    private SettingsFlyoutViewModel _settings = null!;
    private AudioClip? _source;

    public AlertSoundEditorWindow()
    {
        InitializeComponent();
        ClipView.SelectionChanged += (_, _) => ShowRange();
        ClipView.EdgeReleased += (_, end) => PlayEdge(end);
    }

    // One at a time: another "Add from file…" brings the open one forward.
    public static void Open(SettingsFlyoutViewModel settings, Window? owner)
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        _open = new AlertSoundEditorWindow { _settings = settings };
        _open.Closed += (_, _) => _open = null;
        _open.ZoomButton.Content = settings.Localization["AlertEditor_ZoomIn"];
        if (owner is null)
            _open.Show();
        else
            _open.Show(owner);
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await ChooseFileAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        AlertSoundPlayer.Stop();
        base.OnClosed(e);
    }

    private async Task ChooseFileAsync()
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _settings.Localization["AlertEditor_Title"],
                FileTypeFilter = [new FilePickerFileType(_settings.Localization["Settings_GroupFrame_AlertFileType"]) { Patterns = SoundFilePatterns }, FilePickerFileTypes.All]
            });
            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path is null)
            {
                if (_source is null)
                    Close();
                return;
            }

            StatusText.Text = _settings.Localization["AlertEditor_Reading"];
            StatusText.IsVisible = true;
            var clip = await Task.Run(() => AudioFileDecoder.Decode(path, AlertSounds.MaxSource));
            _source = clip;
            NameBox.Text = Path.GetFileNameWithoutExtension(path);
            ClipView.Load(clip, AlertSounds.MaxLength);
            ZoomButton.Content = _settings.Localization["AlertEditor_ZoomIn"];
            StatusText.IsVisible = false;
            EditorPanel.IsVisible = true;
            PlayButton.IsEnabled = SaveButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Reading a sound file for the boss alert failed: {ex}");
            StatusText.Text = _settings.Localization["AlertEditor_Unreadable"];
            StatusText.IsVisible = true;
        }
    }

    private void ShowRange()
    {
        if (_source is null)
            return;

        var rate = (double)_source.SampleRate;
        RangeText.Text = string.Create(CultureInfo.CurrentCulture,
            $"{Time(ClipView.Start / rate)} – {Time(ClipView.End / rate)}   ({(ClipView.End - ClipView.Start) / rate:0.00} s)");

        static string Time(double seconds) => $"{(int)(seconds / 60)}:{seconds % 60:00.00}";
    }

    private void PlayEdge(bool end)
    {
        if (_source is null)
            return;

        var preview = (int)(EdgePreviewSeconds * _source.SampleRate);
        var (start, stop) = end
            ? (Math.Max(ClipView.Start, ClipView.End - preview), ClipView.End)
            : (ClipView.Start, Math.Min(ClipView.End, ClipView.Start + preview));
        AlertSoundPlayer.Play(_source.Cut(start, stop), _settings.GroupFrameAlertVolumePercent);
    }

    private void PlayClicked(object? sender, RoutedEventArgs e)
    {
        if (_source is not null)
            AlertSoundPlayer.Play(_source.Cut(ClipView.Start, ClipView.End), _settings.GroupFrameAlertVolumePercent);
    }

    private async void ChooseClicked(object? sender, RoutedEventArgs e) => await ChooseFileAsync();

    private void ZoomClicked(object? sender, RoutedEventArgs e)
    {
        ClipView.Zoomed = !ClipView.Zoomed;
        ZoomButton.Content = _settings.Localization[ClipView.Zoomed ? "AlertEditor_ShowAll" : "AlertEditor_ZoomIn"];
    }

    private void SaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_source is null)
            return;

        try
        {
            _settings.AddAlertSound(_source.Cut(ClipView.Start, ClipView.End), NameBox.Text ?? string.Empty);
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write(AppLogLevel.Warning, $"Saving the boss alert sound failed: {ex}");
            StatusText.Text = ex.Message;
            StatusText.IsVisible = true;
        }
    }

    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            WindowDrag.Move(this, e);
    }
}
