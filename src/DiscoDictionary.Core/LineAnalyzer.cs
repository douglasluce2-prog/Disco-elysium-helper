using System;
using System.Collections.Generic;

namespace DiscoDictionary.Core;

/// <summary>An entry shown in the "recent terms" sidebar.</summary>
public sealed class RecentTerm
{
    public RecentTerm(GlossaryEntry entry, long lineNumber, bool isSpeaker)
    {
        Entry = entry;
        LineNumber = lineNumber;
        IsSpeaker = isSpeaker;
    }

    public GlossaryEntry Entry { get; }

    /// <summary>The dialogue line number in which the term most recently appeared.</summary>
    public long LineNumber { get; }

    /// <summary>True when the entry was matched from who was speaking rather than what was said.</summary>
    public bool IsSpeaker { get; }
}

/// <summary>
/// Turns each new line of dialogue into a list of glossary entries and keeps a short,
/// newest-first history of them for the sidebar.
/// </summary>
public sealed class LineAnalyzer
{
    private static readonly HashSet<string> IgnoredSpeakers = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "you", "narrator", "narrative", "description",
    };

    private readonly TermMatcher _matcher;
    private readonly SeenStore _seen;
    private readonly List<RecentTerm> _recent = new();

    public LineAnalyzer(TermMatcher matcher, SeenStore seen, int capacity = 10)
    {
        _matcher = matcher;
        _seen = seen;
        Capacity = Math.Max(1, capacity);
    }

    public int Capacity { get; set; }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<RecentTerm> Recent => _recent;

    public long LineCount { get; private set; }

    /// <summary>Incremented whenever <see cref="Recent"/> changes, so the UI knows to redraw.</summary>
    public int Version { get; private set; }

    /// <summary>Entries found in the most recent line (speaker first).</summary>
    public List<GlossaryEntry> LastLineEntries { get; private set; } = new();

    public List<GlossaryEntry> AddLine(string? speaker, string? text, bool includeSpeaker = true)
    {
        LineCount++;
        var found = new List<GlossaryEntry>();
        var speakerEntries = new HashSet<GlossaryEntry>();

        string cleanSpeaker = RichText.CollapseWhitespace(RichText.Strip(speaker)).Trim();
        if (includeSpeaker && !IgnoredSpeakers.Contains(cleanSpeaker))
        {
            foreach (var e in _matcher.FindEntries(cleanSpeaker, ignoreCase: true))
            {
                if (speakerEntries.Add(e))
                    found.Add(e);
            }
        }

        foreach (var e in _matcher.FindEntries(RichText.Strip(text)))
        {
            if (!found.Contains(e))
                found.Add(e);
        }

        LastLineEntries = found;
        if (found.Count == 0)
            return found;

        // Insert in reverse so the first term of the line ends up on top.
        for (int i = found.Count - 1; i >= 0; i--)
        {
            var e = found[i];
            _seen.MarkSeen(e.Id);
            _recent.RemoveAll(r => r.Entry == e);
            _recent.Insert(0, new RecentTerm(e, LineCount, speakerEntries.Contains(e)));
        }

        if (_recent.Count > Capacity)
            _recent.RemoveRange(Capacity, _recent.Count - Capacity);

        Version++;
        return found;
    }

    public void Clear()
    {
        _recent.Clear();
        LastLineEntries = new List<GlossaryEntry>();
        Version++;
    }
}
