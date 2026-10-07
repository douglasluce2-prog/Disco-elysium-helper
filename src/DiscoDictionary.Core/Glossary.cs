using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DiscoDictionary.Core;

/// <summary>The loaded set of entries plus lookup and search.</summary>
public sealed class Glossary
{
    private readonly List<GlossaryEntry> _entries;
    private readonly Dictionary<string, GlossaryEntry> _byId;
    private readonly Dictionary<string, GlossaryEntry> _byName;

    public Glossary(IEnumerable<GlossaryEntry> entries)
    {
        _entries = entries
            .OrderBy(e => SortKey(e.Term), StringComparer.Ordinal)
            .ToList();

        _byId = new Dictionary<string, GlossaryEntry>(StringComparer.OrdinalIgnoreCase);
        _byName = new Dictionary<string, GlossaryEntry>(StringComparer.Ordinal);
        foreach (var e in _entries)
        {
            _byId[e.Id] = e;
            foreach (var name in e.AllNames())
            {
                var key = Normalize(name);
                if (key.Length > 0 && !_byName.ContainsKey(key))
                    _byName[key] = e;
            }
        }
    }

    public IReadOnlyList<GlossaryEntry> Entries => _entries;

    public int Count => _entries.Count;

    public GlossaryEntry? Get(string? id) =>
        id != null && _byId.TryGetValue(id, out var e) ? e : null;

    /// <summary>Exact (accent- and case-insensitive) match on a term or alias.</summary>
    public GlossaryEntry? FindByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return _byName.TryGetValue(Normalize(name!), out var e) ? e : null;
    }

    /// <summary>
    /// Ranked search over names and text. An empty query lists everything alphabetically.
    /// </summary>
    /// <param name="query">What the player typed.</param>
    /// <param name="isVisible">Filter for spoiler-hidden entries; null shows everything.</param>
    /// <param name="category">Restrict to one category; null or empty for all.</param>
    public List<GlossaryEntry> Search(string? query, Func<GlossaryEntry, bool>? isVisible = null, string? category = null)
    {
        string q = Normalize(query ?? "");
        var scored = new List<(GlossaryEntry Entry, int Score)>();

        foreach (var e in _entries)
        {
            if (isVisible != null && !isVisible(e))
                continue;
            if (!string.IsNullOrEmpty(category) && !string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase))
                continue;

            int score = q.Length == 0 ? 1 : Score(e, q);
            if (score > 0)
                scored.Add((e, score));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => SortKey(s.Entry.Term), StringComparer.Ordinal)
            .Select(s => s.Entry)
            .ToList();
    }

    private static int Score(GlossaryEntry e, string q)
    {
        int best = 0;
        bool isTerm = true;
        foreach (var raw in e.AllNames())
        {
            string name = Normalize(raw);
            int bonus = isTerm ? 5 : 0;
            isTerm = false;

            if (name == q)
                best = Math.Max(best, 95 + bonus);
            else if (name.StartsWith(q, StringComparison.Ordinal))
                best = Math.Max(best, 75 + bonus);
            else if (name.Split(' ').Any(w => w.StartsWith(q, StringComparison.Ordinal)))
                best = Math.Max(best, 60 + bonus);
            else if (name.Contains(q))
                best = Math.Max(best, 50 + bonus);
        }

        if (best > 0)
            return best;
        if (Normalize(e.Short).Contains(q))
            return 30;
        if (Normalize(e.Details).Contains(q))
            return 20;
        if (Normalize(e.InspiredBy).Contains(q))
            return 15;
        return 0;
    }

    /// <summary>Lower-case, accent-free, hyphens and punctuation folded to single spaces.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Fold accents with our own table rather than string.Normalize: inside the game (MelonLoader's
        // .NET runtime) Unicode normalization can be unavailable, which once turned "René" into the id
        // "rené-arnoux" so links to "rene-arnoux" broke.
        var sb = new StringBuilder(text!.Length);
        bool lastSpace = true;
        foreach (char raw in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark)
                continue;
            char c = FoldAccent(raw);

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
                lastSpace = false;
            }
            else if (Tokenizer.IsApostrophe(c))
            {
                // "Revachol's" -> "revachols": drop apostrophes entirely.
            }
            else if (!lastSpace)
            {
                sb.Append(' ');
                lastSpace = true;
            }
        }
        return sb.ToString().Trim();
    }

    private const string Accented = "àáâãäåāăąçćčďèéêëēėęěìíîïīįłñńňòóôõöøōőùúûüūůűųýÿžźżšśşťţŕřğ";
    private const string Plain    = "aaaaaaaaacccdeeeeeeeeiiiiiilnnnoooooooouuuuuuuuyyzzzsssttrrg";

    /// <summary>Maps a Latin letter with a diacritic to its plain letter, keeping case.</summary>
    public static char FoldAccent(char c)
    {
        if (c < 128)
            return c;
        bool upper = char.IsUpper(c);
        int i = Accented.IndexOf(char.ToLowerInvariant(c));
        if (i < 0)
            return c;
        return upper ? char.ToUpperInvariant(Plain[i]) : Plain[i];
    }

    private static string SortKey(string term)
    {
        var n = Normalize(term);
        return n.StartsWith("the ", StringComparison.Ordinal) ? n.Substring(4) : n;
    }
}
