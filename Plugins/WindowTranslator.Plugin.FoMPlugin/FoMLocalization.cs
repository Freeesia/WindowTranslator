using System.IO.Compression;
using Tomlyn;
using Tomlyn.Model;

namespace WindowTranslator.Plugin.FoMPlugin;

internal static class FoMLocalization
{
    private const string LocalizationManifestEntryName = "assets/localization/l10n.meta.toml";
    private const string TranslationEntryPrefix = "assets/localization/translations/";
    private const string TranslationEntrySuffix = ".meta.toml";
    private const string T2EntryPrefix = "assets/t2/";
    private const string ConversationEntrySuffix = ".c.toml";
    private const string FiddleEntryPrefix = "assets/fiddle/";
    private const string TomlEntrySuffix = ".toml";

    public static Localization? Load(string archivePath, string? translationCode)
    {
        if (!File.Exists(archivePath))
        {
            return null;
        }

        using var stream = File.OpenRead(archivePath);
        return Load(stream, translationCode);
    }

    internal static Localization? Load(Stream stream, string? translationCode)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var fiddleRenames = ReadFiddleRenames(archive.GetEntry(LocalizationManifestEntryName));
        if (fiddleRenames is null)
        {
            return null;
        }

        var eng = new Dictionary<string, string>(StringComparer.Ordinal);
        var speakers = new Dictionary<string, string>(StringComparer.Ordinal);
        ReadT2Localization(archive, eng, speakers);
        ReadFiddleLocalization(archive, fiddleRenames, eng);
        if (eng.Count == 0)
        {
            return null;
        }

        var translation = string.IsNullOrEmpty(translationCode)
            ? []
            : ReadAssetProperties(archive.GetEntry($"{TranslationEntryPrefix}{translationCode}{TranslationEntrySuffix}")) ?? [];
        return new(eng, translation, speakers);
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

    private static void ReadT2Localization(
        ZipArchive archive,
        Dictionary<string, string> localization,
        Dictionary<string, string> speakers)
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
            var fileSpeaker = GetFileSpeaker(fileKey);
            foreach (var conversation in document.Where(property => property.Value is TomlTable))
            {
                var conversationKey = $"{fileKey}/{conversation.Key}";
                var conversationTable = (TomlTable)conversation.Value;
                var currentSpeaker = GetExplicitSpeaker(conversationTable)
                    ?? fileSpeaker
                    ?? GetRequiredNpc(conversationTable);
                ReadConversationEntry(conversationTable, conversationKey, "init", currentSpeaker, localization, speakers);

                foreach (var sequence in conversationTable.Where(property => property.Value is TomlTable))
                {
                    var sequenceTable = (TomlTable)sequence.Value;
                    currentSpeaker = GetExplicitSpeaker(sequenceTable) ?? currentSpeaker;
                    ReadConversationEntry(sequenceTable, conversationKey, sequence.Key, currentSpeaker, localization, speakers);
                }
            }
        }
    }

    private static void ReadConversationEntry(
        TomlTable entry,
        string conversationKey,
        string sequenceKey,
        string? speaker,
        Dictionary<string, string> localization,
        Dictionary<string, string> speakers)
    {
        if (entry.TryGetValue("local", out var local))
        {
            AddConversationText(localization, speakers, $"{conversationKey}/{sequenceKey}", local, speaker);
        }

        if (entry.TryGetValue("prompts", out var prompts))
        {
            var index = 0;
            foreach (var prompt in EnumerateTables(prompts))
            {
                if (prompt.TryGetValue("local", out var promptLocal) &&
                    promptLocal is string text)
                {
                    var key = $"{conversationKey}/{sequenceKey}/prompts/{index}";
                    localization[key] = text;
                    speakers[key] = "Ari";
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
        Dictionary<string, string> speakers,
        string key,
        object? value,
        string? speaker)
    {
        if (value is string text)
        {
            localization[key] = text;
            AddSpeaker(speakers, key, speaker);
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
            AddSpeaker(speakers, sequenceKey, speaker);
        }
    }

    private static void AddSpeaker(Dictionary<string, string> speakers, string key, string? speaker)
    {
        if (!string.IsNullOrWhiteSpace(speaker))
        {
            speakers[key] = speaker;
        }
    }

    private static string? GetFileSpeaker(string fileKey)
    {
        var parts = fileKey.Split('/');
        return parts is ["Conversations", "Bank", var speaker, ..] ? speaker : null;
    }

    private static string? GetExplicitSpeaker(TomlTable entry)
    {
        if (entry.TryGetValue("speaker", out var speaker) && speaker is string speakerName)
        {
            return speakerName;
        }

        return null;
    }

    private static string? GetRequiredNpc(TomlTable entry)
    {
        if (!entry.TryGetValue("requires", out var requires))
        {
            return null;
        }

        var npcNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectNpcRequirements(requires, npcNames);
        return npcNames.Count == 1 ? npcNames.Single() : null;
    }

    private static void CollectNpcRequirements(object? value, HashSet<string> npcNames)
    {
        switch (value)
        {
            case TomlTable table:
                if (table.TryGetValue("npc", out var npc) && npc is string npcName)
                {
                    npcNames.Add(npcName);
                }

                foreach (var property in table.Values)
                {
                    CollectNpcRequirements(property, npcNames);
                }
                break;

            case TomlArray array:
                foreach (var item in array)
                {
                    CollectNpcRequirements(item, npcNames);
                }
                break;

            case TomlTableArray tableArray:
                foreach (var table in tableArray)
                {
                    CollectNpcRequirements(table, npcNames);
                }
                break;
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
