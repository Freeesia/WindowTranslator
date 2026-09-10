using System.IO.Compression;
using Tomlyn;
using Tomlyn.Model;

namespace WindowTranslator.Plugin.FoMPlugin;

internal static class FoMLocalization
{
    private const string LocalizationManifestEntryName = "assets/localization/l10n.meta.toml";
    private const string JapaneseTranslationEntryName = "assets/localization/translations/jpn.meta.toml";
    private const string T2EntryPrefix = "assets/t2/";
    private const string ConversationEntrySuffix = ".c.toml";
    private const string FiddleEntryPrefix = "assets/fiddle/";
    private const string TomlEntrySuffix = ".toml";

    public static Localization? Load(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            return null;
        }

        using var stream = File.OpenRead(archivePath);
        return Load(stream);
    }

    internal static Localization? Load(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var fiddleRenames = ReadFiddleRenames(archive.GetEntry(LocalizationManifestEntryName));
        if (fiddleRenames is null)
        {
            return null;
        }

        var eng = new Dictionary<string, string>(StringComparer.Ordinal);
        ReadT2Localization(archive, eng);
        ReadFiddleLocalization(archive, fiddleRenames, eng);
        if (eng.Count == 0)
        {
            return null;
        }

        return new(eng, ReadAssetProperties(archive.GetEntry(JapaneseTranslationEntryName)) ?? []);
    }

    private static Dictionary<string, string[]>? ReadFiddleRenames(ZipArchiveEntry? entry)
    {
        var document = ReadToml(entry);
        if (document?["asset_properties"] is not TomlTable assetProperties ||
            assetProperties["fiddle_renames"] is not TomlTable fiddleRenames)
        {
            return null;
        }

        return fiddleRenames
            .Where(property => property.Value is TomlArray)
            .ToDictionary(
                property => property.Key,
                property => ((TomlArray)property.Value).OfType<string>().ToArray(),
                StringComparer.Ordinal);
    }

    private static void ReadT2Localization(ZipArchive archive, Dictionary<string, string> localization)
    {
        foreach (var entry in archive.Entries.Where(entry =>
                     entry.FullName.StartsWith(T2EntryPrefix, StringComparison.Ordinal) &&
                     entry.FullName.EndsWith(ConversationEntrySuffix, StringComparison.Ordinal)))
        {
            var document = ReadToml(entry);
            if (document is null)
            {
                continue;
            }

            var fileKey = entry.FullName[T2EntryPrefix.Length..^ConversationEntrySuffix.Length];
            foreach (var conversation in document.Where(property => property.Value is TomlTable))
            {
                var conversationKey = $"{fileKey}/{conversation.Key}";
                var conversationTable = (TomlTable)conversation.Value;
                ReadConversationEntry(conversationTable, conversationKey, "init", localization);

                foreach (var sequence in conversationTable.Where(property => property.Value is TomlTable))
                {
                    ReadConversationEntry((TomlTable)sequence.Value, conversationKey, sequence.Key, localization);
                }
            }
        }
    }

    private static void ReadConversationEntry(
        TomlTable entry,
        string conversationKey,
        string sequenceKey,
        Dictionary<string, string> localization)
    {
        if (entry.TryGetValue("local", out var local))
        {
            AddConversationText(localization, $"{conversationKey}/{sequenceKey}", local);
        }

        if (entry.TryGetValue("prompts", out var prompts))
        {
            var index = 0;
            foreach (var prompt in EnumerateTables(prompts))
            {
                if (prompt.TryGetValue("local", out var promptLocal) &&
                    promptLocal is string text)
                {
                    localization[$"{conversationKey}/{sequenceKey}/prompts/{index}"] = text;
                }

                index++;
            }
        }
    }

    private static IEnumerable<TomlTable> EnumerateTables(object? value)
    {
        switch (value)
        {
            case TomlArray array:
                foreach (var table in array.OfType<TomlTable>())
                {
                    yield return table;
                }
                break;

            case TomlTableArray tableArray:
                foreach (var table in tableArray)
                {
                    yield return table;
                }
                break;
        }
    }

    private static void AddConversationText(
        Dictionary<string, string> localization,
        string key,
        object? value)
    {
        if (value is string text)
        {
            localization[key] = text;
            return;
        }

        if (value is not TomlArray sequence)
        {
            return;
        }

        for (var index = 0; index < sequence.Count; index++)
        {
            if (sequence[index] is not string sequenceText)
            {
                continue;
            }

            var sequenceKey = index == 0 ? key : $"{key}$_sequence_entry_{index}$";
            localization[sequenceKey] = sequenceText;
        }
    }

    private static void ReadFiddleLocalization(
        ZipArchive archive,
        IReadOnlyDictionary<string, string[]> fiddleRenames,
        Dictionary<string, string> localization)
    {
        foreach (var (source, patterns) in fiddleRenames)
        {
            foreach (var entry in GetFiddleEntries(archive, source))
            {
                var document = ReadToml(entry);
                if (document is null)
                {
                    continue;
                }

                var fileKey = entry.FullName[FiddleEntryPrefix.Length..^TomlEntrySuffix.Length];
                foreach (var (propertyPath, value) in EnumerateStrings(document))
                {
                    if (patterns.Any(pattern => MatchesPattern(propertyPath, pattern)))
                    {
                        localization[$"{fileKey}/{propertyPath}"] = value;
                    }
                }
            }
        }
    }

    private static IEnumerable<ZipArchiveEntry> GetFiddleEntries(ZipArchive archive, string source)
    {
        var path = source.TrimEnd('/');
        if (!source.EndsWith("/", StringComparison.Ordinal))
        {
            var exactEntry = archive.GetEntry($"{FiddleEntryPrefix}{path}{TomlEntrySuffix}");
            if (exactEntry is not null)
            {
                yield return exactEntry;
                yield break;
            }
        }

        var directoryPrefix = $"{FiddleEntryPrefix}{path}/";
        foreach (var entry in archive.Entries.Where(entry =>
                     entry.FullName.StartsWith(directoryPrefix, StringComparison.Ordinal) &&
                     entry.FullName.EndsWith(TomlEntrySuffix, StringComparison.Ordinal)))
        {
            yield return entry;
        }
    }

    private static IEnumerable<(string Path, string Value)> EnumerateStrings(object? value, string path = "")
    {
        switch (value)
        {
            case string text:
                yield return (path, text);
                break;

            case TomlTable table:
                foreach (var property in table)
                {
                    var childPath = string.IsNullOrEmpty(path) ? property.Key : $"{path}/{property.Key}";
                    foreach (var item in EnumerateStrings(property.Value, childPath))
                    {
                        yield return item;
                    }
                }
                break;

            case TomlArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    var childPath = string.IsNullOrEmpty(path) ? index.ToString() : $"{path}/{index}";
                    foreach (var item in EnumerateStrings(array[index], childPath))
                    {
                        yield return item;
                    }
                }
                break;

            case TomlTableArray tableArray:
                for (var index = 0; index < tableArray.Count; index++)
                {
                    var childPath = string.IsNullOrEmpty(path) ? index.ToString() : $"{path}/{index}";
                    foreach (var item in EnumerateStrings(tableArray[index], childPath))
                    {
                        yield return item;
                    }
                }
                break;
        }
    }

    private static bool MatchesPattern(string propertyPath, string pattern)
    {
        var pathParts = propertyPath.Split('/');
        var patternParts = pattern.Split('/');
        return pathParts.Length == patternParts.Length &&
               pathParts.Zip(patternParts).All(parts => parts.Second == "*" || parts.First == parts.Second);
    }

    private static Dictionary<string, string>? ReadAssetProperties(ZipArchiveEntry? entry)
    {
        var document = ReadToml(entry);
        if (document?["asset_properties"] is not TomlTable properties)
        {
            return null;
        }

        return properties
            .Where(property => property.Value is string)
            .ToDictionary(property => property.Key, property => (string)property.Value!, StringComparer.Ordinal);
    }

    private static TomlTable? ReadToml(ZipArchiveEntry? entry)
    {
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        return TomlSerializer.Deserialize<TomlTable>(stream);
    }
}
