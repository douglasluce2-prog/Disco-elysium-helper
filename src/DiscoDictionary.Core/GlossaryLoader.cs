using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DiscoDictionary.Core;

public sealed class GlossaryLoadResult
{
    public GlossaryLoadResult(Glossary glossary, List<string> warnings)
    {
        Glossary = glossary;
        Warnings = warnings;
    }

    public Glossary Glossary { get; }

    /// <summary>Human-readable problems found while loading. Loading never throws for bad data.</summary>
    public List<string> Warnings { get; }
}

/// <summary>
/// Reads glossary JSON files. Later sources override earlier ones entry-by-entry (matched on id),
/// so a player can correct or extend the built-in glossary with their own file.
/// </summary>
public static class GlossaryLoader
{
    public const int MaxShortLength = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Parses one file. Throws <see cref="JsonException"/> on malformed JSON.</summary>
    public static GlossaryFile ParseFile(string json, string sourceName)
    {
        var file = JsonSerializer.Deserialize<GlossaryFile>(json, JsonOptions) ?? new GlossaryFile();
        file.Entries ??= new List<GlossaryEntry>();
        foreach (var e in file.Entries)
        {
            if (e == null)
                continue;
            e.Source = sourceName;
            e.Aliases ??= new List<string>();
            e.SeeAlso ??= new List<string>();
            e.Term = (e.Term ?? "").Trim();
            e.Short = (e.Short ?? "").Trim();
            e.Details = (e.Details ?? "").Trim();
            e.InspiredBy = (e.InspiredBy ?? "").Trim();
            e.Spoiler = (e.Spoiler ?? "").Trim();
            if (string.IsNullOrWhiteSpace(e.Category))
                e.Category = file.Category ?? "";
            e.Category = Categories.Canonical(e.Category.Trim());
            if (string.IsNullOrWhiteSpace(e.Id))
                e.Id = MakeId(e.Term);
        }
        file.Entries.RemoveAll(e => e == null);
        return file;
    }

    /// <summary>Loads and merges several files, collecting warnings instead of failing.</summary>
    public static GlossaryLoadResult Load(IEnumerable<KeyValuePair<string, string>> sources)
    {
        var warnings = new List<string>();
        var byId = new Dictionary<string, GlossaryEntry>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        foreach (var source in sources)
        {
            GlossaryFile file;
            try
            {
                file = ParseFile(source.Value, source.Key);
            }
            catch (JsonException ex)
            {
                warnings.Add($"{source.Key}: could not be read and was skipped ({ex.Message})");
                continue;
            }

            var idsInThisFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in file.Entries)
            {
                if (string.IsNullOrWhiteSpace(e.Term))
                {
                    warnings.Add($"{source.Key}: an entry has no \"term\" and was skipped");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(e.Short))
                    warnings.Add($"{source.Key}: '{e.Term}' has no \"short\" definition");
                if (e.Short.Length > MaxShortLength)
                    warnings.Add($"{source.Key}: '{e.Term}' has a \"short\" longer than {MaxShortLength} characters");
                if (!Categories.IsKnown(e.Category))
                    warnings.Add($"{source.Key}: '{e.Term}' has unknown category '{e.Category}'");
                if (!idsInThisFile.Add(e.Id))
                    warnings.Add($"{source.Key}: id '{e.Id}' is used twice in the same file");

                if (!byId.ContainsKey(e.Id))
                    order.Add(e.Id);
                byId[e.Id] = e;
            }
        }

        var entries = order.Select(id => byId[id]).ToList();
        CheckReferences(entries, byId, warnings);
        CheckNameCollisions(entries, warnings);
        return new GlossaryLoadResult(new Glossary(entries), warnings);
    }

    public static string MakeId(string? term)
    {
        var normalized = Glossary.Normalize(term);
        var sb = new StringBuilder(normalized.Length);
        foreach (char c in normalized)
            sb.Append(c == ' ' ? '-' : c);
        return sb.ToString();
    }

    private static void CheckReferences(List<GlossaryEntry> entries, Dictionary<string, GlossaryEntry> byId, List<string> warnings)
    {
        foreach (var e in entries)
        {
            foreach (var reference in e.SeeAlso)
            {
                if (!byId.ContainsKey(reference))
                    warnings.Add($"{e.Source}: '{e.Term}' has seeAlso '{reference}', which does not exist");
            }
        }
    }

    private static void CheckNameCollisions(List<GlossaryEntry> entries, List<string> warnings)
    {
        // Two different entries claiming the same name means one of them can never be auto-detected.
        var owner = new Dictionary<string, GlossaryEntry>(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            foreach (var name in e.AllNames())
            {
                string key = string.Join(" ", Tokenizer.Tokenize(name).Select(t => t.Key));
                if (key.Length == 0)
                    continue;
                if (owner.TryGetValue(key, out var other) && other != e)
                    warnings.Add($"'{name}' is a name of both '{other.Id}' and '{e.Id}'");
                else
                    owner[key] = e;
            }
        }
    }
}
