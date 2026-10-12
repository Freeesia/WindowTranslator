using System.Diagnostics;
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
    private readonly Stopwatch playbackClock = new();
    private TraceReplay? replay;
    private long playbackStartTicks;
    private bool advancingPlayback;

    public MainWindow()
    {
        InitializeComponent();
        TargetSettings defaults = new();
        GeometrySlider.Value = defaults.OcrGeometryStability;
        RecognitionSlider.Value = defaults.OcrRecognitionStability;
        RetentionSlider.Value = defaults.OcrMissingFrameRetention;
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
            playbackClock.Reset();
            playbackStartTicks = replay.Results[0].Source.RelativeTimeTicks;
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
            if (!advancingPlayback)
            {
                bool playing = playbackClock.IsRunning;
                playbackTimer.Stop();
                playbackClock.Reset();
                playbackStartTicks = replay.Results[(int)SeekSlider.Value].Source.RelativeTimeTicks;
                if (playing)
                {
                    playbackClock.Start();
                }
            }
            UpdateDisplay();
            if (!advancingPlayback && playbackClock.IsRunning)
            {
                Playback_Tick(null, EventArgs.Empty);
            }
        }
    }

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
        if (playbackClock.IsRunning)
        {
            PausePlayback();
            return;
        }
        if (SeekSlider.Value == SeekSlider.Maximum)
        {
            SeekSlider.Value = 0;
        }
        playbackClock.Start();
        PlayButton.Content = "一時停止";
        Playback_Tick(null, EventArgs.Empty);
    }

    private void Playback_Tick(object? sender, EventArgs e)
    {
        playbackTimer.Stop();
        if (replay is null)
        {
            return;
        }

        int index = (int)SeekSlider.Value;
        // UI の描画が遅れても、次のフレームの記録時刻を基準にして遅れを累積させない。
        while (index < replay.Results.Count - 1
            && replay.Results[index + 1].Source.RelativeTimeTicks - playbackStartTicks <= playbackClock.Elapsed.Ticks)
        {
            index++;
        }
        advancingPlayback = true;
        SeekSlider.Value = index;
        advancingPlayback = false;
        if (index == replay.Results.Count - 1)
        {
            PausePlayback();
            return;
        }

        long remainingTicks = replay.Results[index + 1].Source.RelativeTimeTicks
            - playbackStartTicks - playbackClock.Elapsed.Ticks;
        playbackTimer.Interval = TimeSpan.FromTicks(Math.Max(0, remainingTicks));
        playbackTimer.Start();
    }

    private void PausePlayback()
    {
        playbackTimer.Stop();
        playbackClock.Stop();
        PlayButton.Content = "再生";
    }

    private void UpdateDisplay()
    {
        bool overlay = ModeSelector.SelectedIndex == 3;
        bool showRaw = ModeSelector.SelectedIndex != 1;
        bool showTracked = ModeSelector.SelectedIndex is 1 or 2;
        RawLabel.Text = overlay ? "重ね合わせ（Raw OCR: 水色 / Tracked: オレンジ）" : "Raw OCR";
        RawPanel.Visibility = showRaw ? Visibility.Visible : Visibility.Collapsed;
        TrackedPanel.Visibility = showTracked ? Visibility.Visible : Visibility.Collapsed;
        RawColumn.Width = showRaw ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        TrackedColumn.Width = showTracked ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SeekSlider.IsEnabled = replay is not null;
        PlayButton.IsEnabled = replay is not null && replay.Results.Count > 1;
        if (replay is null)
        {
            FrameText.Text = "フレーム 0 / 0";
            return;
        }

        int index = (int)SeekSlider.Value;
        ReplayFrame frame = replay.Results[index];
        RawSurface.SetFrame(frame.Source.ImageWidth, frame.Source.ImageHeight, frame.Raw, overlay ? frame.Tracked : []);
        TrackedSurface.SetFrame(frame.Source.ImageWidth, frame.Source.ImageHeight, [], frame.Tracked);
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
        playbackClock.Stop();
        base.OnClosed(e);
    }
}
