using System.Drawing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Windows.Graphics.Imaging;
using WindowTranslator.Modules;
using WindowTranslator.Modules.Ocr;
using Xunit.Abstractions;

namespace WindowTranslator.Tests;

public class OcrBufferFilterAccuracyTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ExistingBufferFilterAccuracyBaseline()
    {
        var actual = new List<IReadOnlyList<TextRect>>();

        foreach (var scenario in OcrTrackingAccuracyScenarios.All)
        {
            var filter = new OcrBufferFilter(
                new Snapshot<TargetSettings>(new()),
                new Snapshot<BasicOcrParam>(new()
                {
                    BufferSize = 3,
                    IsEnableRecover = true,
                    IsSuppressVibe = true,
                }),
                NullLogger<OcrBufferFilter>.Instance);

            using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 1000, 600, BitmapAlphaMode.Premultiplied);
            var context = new FilterContext { SoftwareBitmap = bitmap, ImageSize = new Size(1000, 600) };
            foreach (var frame in scenario.Observations)
            {
                actual.Add(await filter.ExecutePreTranslate(frame.ToAsyncEnumerable(), context).ToArrayAsync());
            }
        }

        var accuracy = OcrTrackingAccuracyScenarios.Measure(actual);
        output.WriteLine($"OcrBufferFilter baseline accuracy: {accuracy:P2}");
        Assert.InRange(accuracy, 0.8005, 0.8007);
    }
}

internal sealed class Snapshot<T>(T value) : IOptionsSnapshot<T>
    where T : class
{
    public T Value { get; } = value;

    public T Get(string? name) => this.Value;
}
