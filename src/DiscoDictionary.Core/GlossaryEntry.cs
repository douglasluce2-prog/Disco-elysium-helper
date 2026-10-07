using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DiscoDictionary.Core;

/// <summary>
/// One dictionary entry, e.g. "Revachol" or "Inland Empire".
/// Everything except <see cref="Spoiler"/> must be written so it is safe to read
/// at any point in the game.
/// </summary>
public sealed class GlossaryEntry
{
    /// <summary>Stable identifier used for links and "seen" tracking. Derived from <see cref="Term"/> when omitted.</summary>
    public string Id { get; set; } = "";

    /// <summary>The headword shown to the player.</summary>
    public string Term { get; set; } = "";

    /// <summary>Other spellings, plurals, demonyms or nicknames that should also be recognised in game text.</summary>
    public List<string> Aliases { get; set; } = new();

    /// <summary>One of <see cref="Categories.All"/>. Inherited from the file when omitted.</summary>
    public string Category { get; set; } = "";

    /// <summary>One-line, spoiler-free definition shown in the sidebar.</summary>
    public string Short { get; set; } = "";

    /// <summary>Longer spoiler-free explanation. Paragraphs are separated by blank lines.</summary>
    public string Details { get; set; } = "";

    /// <summary>Optional: the real-world thing this is based on or satirising.</summary>
    public string InspiredBy { get; set; } = "";

    /// <summary>Optional: plot information. Hidden until the player clicks to reveal it.</summary>
    public string Spoiler { get; set; } = "";

    /// <summary>Ids of related entries.</summary>
    public List<string> SeeAlso { get; set; } = new();

    /// <summary>
    /// When true the term is only recognised with the same capitalisation (or in ALL CAPS).
    /// Use for names that are also ordinary English words: "the Pale" vs "pale", "Logic" vs "logic".
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// For case-sensitive names that are also everyday words ("Logic", "Endurance", "Pale"):
    /// don't match a single capitalised word at the start of a sentence, where any word is capitalised.
    /// </summary>
    public bool CommonWord { get; set; }

    /// <summary>When false the entry is only reachable through search/links, never auto-detected in dialogue.</summary>
    public bool AutoDetect { get; set; } = true;

    /// <summary>
    /// When true the entry is not listed in the dictionary until the player has met the term in game
    /// (because merely knowing the name would be a spoiler).
    /// </summary>
    public bool HideUntilSeen { get; set; }

    /// <summary>Which file the entry was loaded from (for warnings). Not part of the JSON format.</summary>
    [JsonIgnore]
    public string Source { get; set; } = "";

    public IEnumerable<string> AllNames()
    {
        yield return Term;
        foreach (var alias in Aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
                yield return alias;
        }
    }

    public override string ToString() => $"{Term} ({Id})";
}

/// <summary>Shape of one glossary JSON file.</summary>
public sealed class GlossaryFile
{
    /// <summary>Default category for entries in this file that don't set their own.</summary>
    public string Category { get; set; } = "";

    public List<GlossaryEntry> Entries { get; set; } = new();
}
