using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using WindowTranslator.Plugin.OrcaRouterPlugin.Properties;

namespace WindowTranslator.Plugin.OrcaRouterPlugin;

public sealed record OrcaRouterModelItem(string Value, string DisplayName);

internal static class OrcaRouterModels
{
    public const string Endpoint = "https://api.orcarouter.ai/v1";
    public const string AutoModel = "orcarouter/auto";

    public static async Task<IReadOnlyList<OrcaRouterModelItem>> GetItemsAsync(HttpClient client, string? apiKey, string selectedModel, CancellationToken cancellationToken)
    {
        var items = new List<OrcaRouterModelItem> { CreateAutoItem() };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Endpoint}/models");
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            items.AddRange(ParseModels(json.RootElement).OrderBy(i => i.DisplayName, StringComparer.CurrentCulture));
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or InvalidOperationException or OperationCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            // オフラインでも自動選択と保存済みモデルを維持する。
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrEmpty(selectedModel) && !items.Any(i => Equals(i.Value, selectedModel)))
        {
            items.Add(new(selectedModel, selectedModel));
        }
        return items;
    }

    internal static OrcaRouterModelItem CreateAutoItem()
        => new(AutoModel, $"OrcaRouter Auto ({Resources.Text("VariablePrice")})");

    internal static IEnumerable<OrcaRouterModelItem> ParseModels(JsonElement root)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { AutoModel };
        foreach (var model in root.GetProperty("data").EnumerateArray())
        {
            if (!model.TryGetProperty("id", out var idValue) || idValue.ValueKind != JsonValueKind.String
                || idValue.GetString() is not { Length: > 0 } id || !seen.Add(id))
            {
                continue;
            }
            // Chat Completions で扱えない画像生成・音声・埋め込み専用モデルは候補に含めない。
            if (model.TryGetProperty("supported_endpoint_types", out var endpoints)
                && !endpoints.EnumerateArray().Any(e => e.GetString() == "openai"))
            {
                continue;
            }
            if (model.TryGetProperty("architecture", out var architecture)
                && architecture.TryGetProperty("output_modalities", out var outputs)
                && !outputs.EnumerateArray().Any(e => e.GetString() == "text"))
            {
                continue;
            }
            var name = model.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
            var price = Resources.Text("PriceUnavailable");
            if (model.TryGetProperty("pricing", out var pricing))
            {
                var input = GetPrice(pricing, "prompt");
                var output = GetPrice(pricing, "completion");
                if (input is not null && output is not null)
                {
                    price = FormattableString.Invariant($"${input:0.######} / ${output:0.######} per 1M");
                }
            }
            yield return new(id, $"{(string.IsNullOrWhiteSpace(name) ? id : name)} ({price})");
        }
    }

    private static decimal? GetPrice(JsonElement pricing, string name)
    {
        if (pricing.TryGetProperty(name + "_per_million", out var perMillion) && ReadDecimal(perMillion) is { } amount)
        {
            return amount;
        }
        return pricing.TryGetProperty(name, out var perToken) ? ReadDecimal(perToken) * 1_000_000m : null;
    }

    private static decimal? ReadDecimal(JsonElement element)
        => decimal.TryParse(element.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : null;
}
