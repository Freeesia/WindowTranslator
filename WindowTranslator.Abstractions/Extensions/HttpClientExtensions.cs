using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace WindowTranslator.Extensions;

/// <summary>
/// <see cref="HttpClient"/> の拡張メソッドを提供します。
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// 指定したURIからファイルを非同期でダウンロードし、ダウンロードの進捗状況を1秒ごとに報告します。
    /// </summary>
    /// <param name="httpClient">HTTPクライアント。</param>
    /// <param name="uri">ダウンロード元のURI。</param>
    /// <param name="path">ダウンロード先のファイルパス。</param>
    /// <param name="progress">ダウンロードの進捗状況を受け取るコールバック。進捗率は0.0～1.0の範囲で報告されます。</param>
    /// <param name="token">キャンセルトークン</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="HttpRequestException">HTTPリクエストが失敗した場合にスローされます。</exception>
    /// <remarks>
    /// このメソッドは以下の動作を行います：
    /// <list type="bullet">
    /// <item><description>Content-Lengthヘッダーが存在する場合、1秒ごとに進捗率を計算して報告します。</description></item>
    /// <item><description>Content-Lengthヘッダーが存在しない場合、ダウンロード完了時にのみ100%を報告します。</description></item>
    /// <item><description>80KBのバッファサイズでストリーミングダウンロードを実行します。</description></item>
    /// <item><description>ダウンロード完了時には必ず100%（1.0f）の進捗を報告します。</description></item>
    /// </list>
    /// </remarks>
    public static async ValueTask DownloadFile(this HttpClient httpClient, [StringSyntax("Uri")] string uri, string path, Action<float> progress, CancellationToken token = default)
    {
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (!contentLength.HasValue)
        {
            // Content-Lengthがない場合は通常のダウンロード
            await using var stream = File.Create(path);
            await response.Content.CopyToAsync(stream, token).ConfigureAwait(false);
            progress(1.0f);
            return;
        }

        var totalBytes = contentLength.Value;
        using var buffer = MemoryPool<byte>.Shared.Rent(81920); // 80KB バッファ
        var totalBytesRead = 0L;
        var lastProgressReport = DateTime.UtcNow;
        var progressReportInterval = TimeSpan.FromSeconds(1);

        await using var contentStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var fileStream = File.Create(path);

        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer.Memory, token).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.Memory[..bytesRead], token).ConfigureAwait(false);
            totalBytesRead += bytesRead;

            var now = DateTime.UtcNow;
            if (now - lastProgressReport >= progressReportInterval)
            {
                var currentProgress = (float)totalBytesRead / totalBytes;
                progress(currentProgress);
                lastProgressReport = now;
            }
        }

        // 最終的な進捗を報告
        progress(1.0f);
    }
}
