using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WindowTranslator.TraceViewer;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer playbackTimer = new();
    private TraceReplay? replay;

    public MainWindow()
    {
        InitializeComponent();
        TargetSettings defaults = new();
        GeometrySlider.Value = defaults.OcrGeometryStability;
        RecognitionSlider.Value = defaults.OcrRecognitionStability;
        RetentionSlider.Value = defaults.OcrMissingFrameRetention;
        playbackTimer.Interval = TimeSpan.FromSeconds(1 / FpsSlider.Value);
        playbackTimer.Tick += Playback_Tick;
        UpdateDisplay();
    }

    private TargetSettings ReadSettings() => new()
    {
        OcrGeometryStability = (int)GeometrySlider.Value,
        OcrRecognitionStability = (int)RecognitionSlider.Value,
        OcrMissingFrameRetention = (int)RetentionSlider.Value,
    };

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "OCR トレース (*.jsonl)|*.jsonl",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        PausePlayback();
        Root.IsEnabled = false;
        Cursor = Cursors.Wait;
        ErrorText.Text = string.Empty;
        FrameText.Text = "読み込み・再計算中…";
        try
        {
            TargetSettings settings = ReadSettings();
            replay = await Task.Run(() => TraceReplay.Load(dialog.FileName, settings));
            FileText.Text = dialog.FileName;
            FileText.ToolTip = dialog.FileName;
            SeekSlider.Maximum = replay.Results.Count - 1;
            SeekSlider.Value = 0;
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            ErrorText.Text = $"トレースを開けませんでした。{error.Message}";
        }
        finally
        {
            Root.IsEnabled = true;
            Cursor = null;
            UpdateDisplay();
        }
    }

    private async void TrackerSettings_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (replay is null)
        {
            return;
        }

        PausePlayback();
        Root.IsEnabled = false;
        Cursor = Cursors.Wait;
        FrameText.Text = "再計算中…";
        try
        {
            TargetSettings settings = ReadSettings();
            await Task.Run(() => replay.Recalculate(settings));
        }
        finally
        {
            Root.IsEnabled = true;
            Cursor = null;
            UpdateDisplay();
        }
    }

    private void DisplayMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateDisplay();
        }
    }

    private void Seek_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (replay is not null)
        {
            UpdateDisplay();
        }
    }

    private void Fps_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        => playbackTimer.Interval = TimeSpan.FromSeconds(1 / e.NewValue);

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        PausePlayback();
        SeekSlider.Value--;
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        PausePlayback();
        SeekSlider.Value++;
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (playbackTimer.IsEnabled)
        {
            PausePlayback();
            return;
        }
        if (SeekSlider.Value == SeekSlider.Maximum)
        {
            SeekSlider.Value = 0;
        }
        playbackTimer.Start();
        PlayButton.Content = "一時停止";
    }

    private void Playback_Tick(object? sender, EventArgs e)
    {
        if (SeekSlider.Value < SeekSlider.Maximum)
        {
            SeekSlider.Value++;
        }
        else
        {
            PausePlayback();
        }
    }

    private void PausePlayback()
    {
        playbackTimer.Stop();
        PlayButton.Content = "再生";
    }

    private void UpdateDisplay()
    {
        bool showRaw = ModeSelector.SelectedIndex != 1;
        bool showTracked = ModeSelector.SelectedIndex != 0;
        RawPanel.Visibility = showRaw ? Visibility.Visible : Visibility.Collapsed;
        TrackedPanel.Visibility = showTracked ? Visibility.Visible : Visibility.Collapsed;
        RawColumn.Width = showRaw ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        TrackedColumn.Width = showTracked ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SeekSlider.IsEnabled = PlayButton.IsEnabled = replay is not null;
        if (replay is null)
        {
            FrameText.Text = "フレーム 0 / 0";
            return;
        }

        int index = (int)SeekSlider.Value;
        ReplayFrame frame = replay.Results[index];
        RawSurface.SetFrame(frame.Source.ImageWidth, frame.Source.ImageHeight, frame.Raw);
        TrackedSurface.SetFrame(frame.Source.ImageWidth, frame.Source.ImageHeight, frame.Tracked);
        PreviousButton.IsEnabled = index > 0;
        NextButton.IsEnabled = index < replay.Results.Count - 1;
        FrameText.Text = $"フレーム {index + 1} / {replay.Results.Count}   "
            + $"相対時刻 {TimeSpan.FromTicks(frame.Source.RelativeTimeTicks):c}   "
            + $"{frame.Source.ImageWidth} × {frame.Source.ImageHeight}";
        if (index == replay.Results.Count - 1)
        {
            PausePlayback();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        playbackTimer.Stop();
        base.OnClosed(e);
    }
}
