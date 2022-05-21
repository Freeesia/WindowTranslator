using CommunityToolkit.Mvvm.ComponentModel;
using PInvoke;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace WindowTranslator;

[ObservableObject]
public partial class MainViewModel
{
    [ObservableProperty]
    private IntPtr windowHandle;

    private readonly Timer timer = new(1000);

    public MainViewModel()
    {
        this.timer.Elapsed += (_, _) => AnalyzeWindow();
        this.timer.Start();
    }

    private async void AnalyzeWindow()
    {
        var sw = Stopwatch.StartNew();
        User32.GetWindowRect(this.WindowHandle, out var rect);

        using var bmp = new Bitmap(rect.right - rect.left, rect.bottom - rect.top);
        using var g = Graphics.FromImage(bmp);

        g.CopyFromScreen(rect.left, rect.top, 0, 0, bmp.Size);

        var sbmp = await ConvertSoftwareBitmap(bmp);
        var ocr = OcrEngine.TryCreateFromLanguage(new("ja-JP"));
        var result = await ocr.RecognizeAsync(sbmp);
        Debug.WriteLine(result.Text);
        Debug.WriteLine(sw.Elapsed);
    }

    private static async Task<SoftwareBitmap> ConvertSoftwareBitmap(Bitmap image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Bmp);

        var irstream = stream.AsRandomAccessStream();

        var decorder = await BitmapDecoder.CreateAsync(irstream);
        return await decorder.GetSoftwareBitmapAsync();

    }
}
