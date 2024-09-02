using System.Drawing;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using StudioFreesia.ColorThief;
using Windows.Graphics.Imaging;

var config = DefaultConfig.Instance
    .AddJob(Job.InProcess);

_ = BenchmarkRunner.Run<ColorThiefTest>(config);

[RPlotExporter]
[MemoryDiagnoser]
public class ColorThiefTest
{
    private readonly ColorThiefDotNet.ColorThief colorThief = new();
    private readonly SoftwareBitmap sbmp;
    private readonly Bitmap bmp;

    public ColorThiefTest()
    {
        using var fileStream = new FileStream(@"images\test2.jpg", FileMode.Open, FileAccess.Read);
        var randomAccessStream = fileStream.AsRandomAccessStream();

        var decoder = BitmapDecoder.CreateAsync(randomAccessStream).AsTask().Result;
        this.sbmp = decoder.GetSoftwareBitmapAsync().AsTask().Result;
        this.bmp = new Bitmap(randomAccessStream.AsStream());
    }

    [Benchmark(Baseline = true)]
    public List<ColorThiefDotNet.QuantizedColor> Original() => this.colorThief.GetPalette(this.bmp);

    [Benchmark]
    public List<QuantizedColor> ColorThiefEx() => ColorThief.GetPalette(this.sbmp);
}