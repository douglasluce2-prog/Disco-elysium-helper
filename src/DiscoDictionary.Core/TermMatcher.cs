using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscoDictionary.Core;

/// <summary>A glossary term found inside a piece of text.</summary>
public sealed class TermMatch
{
    public TermMatch(GlossaryEntry entry, int start, int length, string text)
    {
        Entry = entry;
        Start = start;
        Length = length;
        Text = text;
    }

    public GlossaryEntry Entry { get; }
    public int Start { get; }
    public int Length { get; }
    public int End => Start + Length;

    /// <summary>The exact characters that matched, e.g. "Revachol's".</summary>
    public string Text { get; }

    public override string ToString() => $"{Text} -> {Entry.Id} @{Start}";
}

/// <summary>
/// Finds glossary terms in arbitrary text. Multi-word terms win over their parts
/// ("Revachol Citizens Militia" beats "Revachol"), possessives and simple plurals are understood,
/// and case-sensitive entries ignore lower-case look-alikes ("the Pale" vs "a pale face").
/// </summary>
public sealed class TermMatcher
{
    private sealed class Pattern
    {
        public Pattern(GlossaryEntry entry, string[] words, bool caseSensitive)
        {
            Entry = entry;
            Words = words;
            CaseSensitive = caseSensitive;
            SkipAtSentenceStart = caseSensitive && entry.CommonWord && words.Length == 1;
        }

        public GlossaryEntry Entry { get; }
        public string[] Words { get; }
        public bool CaseSensitive { get; }
        public bool SkipAtSentenceStart { get; }
    }

    private readonly Dictionary<string, List<Pattern>> _patterns = new(StringComparer.Ordinal);
    private readonly int _maxWords;

    /// <param name="entries">Entries to recognise.</param>
    /// <param name="includeManualOnly">Also recognise entries with AutoDetect = false.</param>
    public TermMatcher(IEnumerable<GlossaryEntry> entries, bool includeManualOnly = false)
    {
        foreach (var entry in entries)
        {
            if (!entry.AutoDetect && !includeManualOnly)
                continue;

            foreach (var name in entry.AllNames())
            {
                var words = Tokenizer.Tokenize(name).Select(t => t.Word).ToArray();
                if (words.Length == 0)
                    continue;

                Add(entry, words, entry.CaseSensitive);

                // Simple plural: "Innocence" -> "Innocences", "Hardie Boy" -> "Hardie Boys".
                string last = words[words.Length - 1];
                if (!last.EndsWith("s", StringComparison.OrdinalIgnoreCase) && char.IsLetter(last[last.Length - 1]))
                {
                    var plural = (string[])words.Clone();
                    plural[plural.Length - 1] = last + (IsAllUpper(last) ? "S" : "s");
                    Add(entry, plural, entry.CaseSensitive);
                }

                _maxWords = Math.Max(_maxWords, words.Length);
            }
        }
    }

    public int PatternCount => _patterns.Values.Sum(l => l.Count);

    private void Add(GlossaryEntry entry, string[] words, bool caseSensitive)
    {
        string key = string.Join(" ", words).ToLowerInvariant();
        if (!_patterns.TryGetValue(key, out var list))
        {
            list = new List<Pattern>();
            _patterns[key] = list;
        }

        // The first entry to claim a name keeps it; the loader warns about collisions.
        if (list.Any(p => p.Entry == entry && p.CaseSensitive == caseSensitive && p.Words.SequenceEqual(words)))
            return;
        list.Add(new Pattern(entry, words, caseSensitive));
    }

    /// <summary>Finds every term in <paramref name="text"/>, left to right, without overlaps.</summary>
    /// <param name="ignoreCase">Treat case-sensitive entries as case-insensitive (used for speaker names).</param>
    public List<TermMatch> FindAll(string? text, bool ignoreCase = false)
    {
        var result = new List<TermMatch>();
        if (string.IsNullOrEmpty(text))
            return result;

        var tokens = Tokenizer.Tokenize(text);
        int i = 0;
        while (i < tokens.Count)
        {
            var match = MatchAt(text!, tokens, i, ignoreCase, sentenceStartRule: true, out int consumed);
            if (match != null)
            {
                result.Add(match);
                i += consumed;
            }
            else
            {
                i++;
            }
        }
        return result;
    }

    /// <summary>Returns the term covering the character at <paramref name="charIndex"/>, if any.</summary>
    public TermMatch? FindAt(string? text, int charIndex, bool ignoreCase = false)
    {
        if (string.IsNullOrEmpty(text) || charIndex < 0 || charIndex >= text!.Length)
            return null;

        var tokens = Tokenizer.Tokenize(text);

        // Try every phrase that could include the token under the cursor, longest first,
        // so pointing at "Empire" in "Inland Empire" finds the skill, not nothing.
        int target = tokens.FindIndex(t => charIndex >= t.Start && charIndex < t.End);
        if (target < 0)
            return null;

        TermMatch? best = null;
        int bestWords = 0;
        for (int start = Math.Max(0, target - _maxWords + 1); start <= target; start++)
        {
            // The player pointed at this word on purpose, so a capital at a sentence start is fine.
            var m = MatchAt(text, tokens, start, ignoreCase, sentenceStartRule: false, out int consumed);
            if (m != null && start + consumed > target && consumed > bestWords)
            {
                best = m;
                bestWords = consumed;
            }
        }
        return best;
    }

    /// <summary>Distinct entries mentioned in the text, in order of first appearance.</summary>
    public List<GlossaryEntry> FindEntries(string? text, bool ignoreCase = false)
    {
        var seen = new HashSet<GlossaryEntry>();
        var list = new List<GlossaryEntry>();
        foreach (var m in FindAll(text, ignoreCase))
        {
            if (seen.Add(m.Entry))
                list.Add(m.Entry);
        }
        return list;
    }

    private TermMatch? MatchAt(string text, List<Token> tokens, int index, bool ignoreCase, bool sentenceStartRule, out int consumed)
    {
        consumed = 0;
        int available = 1;
        while (available < _maxWords && index + available < tokens.Count && tokens[index + available].JoinsPrevious)
            available++;

        for (int n = available; n >= 1; n--)
        {
            string key = BuildKey(tokens, index, n);
            if (!_patterns.TryGetValue(key, out var candidates))
                continue;

            foreach (var pattern in candidates)
            {
                if (ignoreCase || !pattern.CaseSensitive || CaseMatches(tokens, index, pattern.Words))
                {
                    if (sentenceStartRule && !ignoreCase && pattern.SkipAtSentenceStart && !IsAllUpper(tokens[index].Word) && IsSentenceStart(text, tokens[index].Start))
                        continue;

                    consumed = n;
                    int start = tokens[index].Start;
                    int end = tokens[index + n - 1].End;
                    return new TermMatch(pattern.Entry, start, end - start, text.Substring(start, end - start));
                }
            }
        }
        return null;
    }

    private static string BuildKey(List<Token> tokens, int index, int count)
    {
        if (count == 1)
            return tokens[index].Key;

        var parts = new string[count];
        for (int k = 0; k < count; k++)
            parts[k] = tokens[index + k].Key;
        return string.Join(" ", parts);
    }

    private static bool CaseMatches(List<Token> tokens, int index, string[] words)
    {
        for (int k = 0; k < words.Length; k++)
        {
            string actual = tokens[index + k].Word;
            string expected = words[k];
            if (string.Equals(actual, expected, StringComparison.Ordinal))
                continue;
            // ALL CAPS renditions (speaker names, headings) are fine too.
            if (string.Equals(actual, expected.ToUpperInvariant(), StringComparison.Ordinal))
                continue;
            return false;
        }
        return true;
    }

    /// <summary>True when the word at <paramref name="start"/> begins a sentence (so its capital letter means nothing).</summary>
    public static bool IsSentenceStart(string text, int start)
    {
        int i = start - 1;
        while (i >= 0)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '\u201C' || c == '\u2018' || c == '(' || c == '\u00AB' || c == '*' || c == '-' || c == '\u2013' || c == '\u2014')
            {
                i--;
                continue;
            }
            return c == '.' || c == '!' || c == '?' || c == '\u2026' || c == ':' || c == ';';
        }
        return true;
    }

    private static bool IsAllUpper(string s) => s.Length > 1 && s.All(c => !char.IsLetter(c) || char.IsUpper(c));
}
