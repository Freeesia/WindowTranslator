using System.Globalization;
using System.Text.RegularExpressions;

namespace WindowTranslator.Modules.PluginStore;

internal static partial class LocalizedReadmeSelector
{
    public static string Select(string markdown, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(culture);

        var headings = LanguageHeadingRegex.Matches(markdown)
            .Select(TryCreateHeading)
            .OfType<LanguageHeading>()
            .ToArray();
        if (headings.Length == 0)
        {
            return markdown;
        }

        var preamble = markdown[..headings[0].Start];
        var sections = headings
            .Select((heading, index) => new ReadmeSection(
                heading.CultureName,
                markdown[heading.ContentStart..(index + 1 < headings.Length
                    ? headings[index + 1].Start
                    : markdown.Length)]))
            .Where(section => !string.IsNullOrWhiteSpace(section.Content))
            .ToArray();
        if (sections.Length == 0)
        {
            return markdown;
        }

        var selected = GetPreferredCultureNames(culture)
            .Select(name => sections.FirstOrDefault(section =>
                section.CultureName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(section => section is not null)
            ?? sections[0];
        return Combine(preamble, selected.Content, markdown);
    }

    private static IEnumerable<string> GetPreferredCultureNames(CultureInfo culture)
    {
        for (var candidate = culture; !string.IsNullOrEmpty(candidate.Name); candidate = candidate.Parent)
        {
            yield return candidate.Name;
        }

        yield return "en";
    }

    private static LanguageHeading? TryCreateHeading(Match match)
    {
        var value = match.Groups["culture"].Value;
        try
        {
            var cultureName = CultureInfo.GetCultureInfo(value).Name;
            return value.Equals(cultureName, StringComparison.Ordinal)
                ? new(match.Index, match.Index + match.Length, cultureName)
                : null;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private static string Combine(string preamble, string content, string markdown)
    {
        var shared = preamble.TrimEnd('\r', '\n');
        var localized = content.Trim('\r', '\n');
        if (shared.Length == 0)
        {
            return localized;
        }
        if (localized.Length == 0)
        {
            return shared;
        }

        var newLine = markdown.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : "\n";
        return $"{shared}{newLine}{newLine}{localized}";
    }

    [GeneratedRegex(
        @"^##[ \t]+(?<culture>[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*)(?:[ \t]+#+)?[ \t]*(?:\r?\n|$)",
        RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex LanguageHeadingRegex { get; }

    private sealed record LanguageHeading(int Start, int ContentStart, string CultureName);

    private sealed record ReadmeSection(string CultureName, string Content);
}
