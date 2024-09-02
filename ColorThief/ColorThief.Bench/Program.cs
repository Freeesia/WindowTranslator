using System.Drawing;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using StudioFreesia.ColorThief;

var config = DefaultConfig.Instance
    .AddJob(Job.InProcess);

_ = BenchmarkRunner.Run<ColorThiefTest>(config);

[RPlotExporter]
[MemoryDiagnoser]
public class ColorThiefTest
{
    private readonly ColorThiefDotNet.ColorThief colorThief = new();
    private readonly Bitmap bmp = new("test2.jpg");

    [Benchmark(Baseline = true)]
    public List<ColorThiefDotNet.QuantizedColor> Original() => this.colorThief.GetPalette(this.bmp);

    [Benchmark]
    public List<QuantizedColor> ColorThiefEx() => ColorThief.GetPalette(bmp);
}