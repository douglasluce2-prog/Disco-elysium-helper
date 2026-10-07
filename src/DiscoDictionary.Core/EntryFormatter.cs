using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscoDictionary.Core;

/// <summary>
/// Builds the TextMeshPro rich text for the dictionary's detail pane and sidebar.
/// Other glossary terms mentioned inside an explanation become clickable links,
/// so the dictionary reads like a small wiki.
/// </summary>
public sealed class EntryFormatter
{
    public const string EntryLinkPrefix = "entry:";
    public const string SpoilerLinkPrefix = "spoiler:";

    public const string HeadingColor = "C9A66B";
    public const string LinkColor = "9CC9F0";
    public const string MutedColor = "8C8C8C";
    public const string SpoilerColor = "E0675A";

    private readonly Glossary _glossary;
    private readonly TermMatcher _linkMatcher;

    public EntryFormatter(Glossary glossary, TermMatcher linkMatcher)
    {
        _glossary = glossary;
        _linkMatcher = linkMatcher;
    }

    /// <summary>When set, links are not created to entries this returns false for (unseen spoiler entries).</summary>
    public Func<GlossaryEntry, bool>? CanLinkTo { get; set; }

    public string FormatEntry(GlossaryEntry e, bool spoilerRevealed)
    {
        var sb = new StringBuilder();
        sb.Append("<size=150%><b>").Append(RichText.Escape(e.Term)).Append("</b></size>\n");
        sb.Append(RichText.Color(Categories.Color(e.Category), "<size=80%>" + RichText.Escape(e.Category.ToUpperInvariant()) + "</size>"));

        var aliases = e.Aliases.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        if (aliases.Count > 0)
        {
            sb.Append("   ").Append(RichText.Color(MutedColor, "<size=80%>also: " + RichText.Escape(string.Join(", ", aliases)) + "</size>"));
        }
        sb.Append("\n\n");

        sb.Append("<i>").Append(Linkify(e.Short, e.Id)).Append("</i>\n");

        if (e.Details.Length > 0)
            sb.Append('\n').Append(Linkify(e.Details, e.Id)).Append('\n');

        if (e.InspiredBy.Length > 0)
        {
            sb.Append('\n').Append(Heading("Real-world inspiration")).Append('\n');
            sb.Append(Linkify(e.InspiredBy, e.Id)).Append('\n');
        }

        var related = e.SeeAlso
            .Select(id => _glossary.Get(id))
            .Where(r => r != null && r != e && (CanLinkTo == null || CanLinkTo(r)))
            .Select(r => r!)
            .ToList();
        if (related.Count > 0)
        {
            sb.Append('\n').Append(Heading("See also: "));
            sb.Append(string.Join(", ", related.Select(r => EntryLink(r, r.Term))));
            sb.Append('\n');
        }

        if (e.Spoiler.Length > 0)
        {
            sb.Append('\n');
            if (spoilerRevealed)
            {
                sb.Append(RichText.Color(SpoilerColor, "<b>Spoiler</b>")).Append('\n');
                sb.Append(Linkify(e.Spoiler, e.Id)).Append('\n');
            }
            else
            {
                sb.Append(RichText.Link(SpoilerLinkPrefix + e.Id,
                    RichText.Color(SpoilerColor, "[ Contains spoilers. Click here to reveal. ]")));
                sb.Append('\n');
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Compact two-line form used in the sidebar.</summary>
    public static string FormatSidebarItem(GlossaryEntry e, bool fresh, bool isSpeaker)
    {
        var sb = new StringBuilder();
        string nameColor = fresh ? "FFFFFF" : "C8C8C8";
        sb.Append(RichText.Color(nameColor, "<b>" + RichText.Escape(e.Term) + "</b>"));
        sb.Append("  ").Append(RichText.Color(Categories.Color(e.Category), "<size=75%>" + RichText.Escape(e.Category.ToUpperInvariant()) + "</size>"));
        if (isSpeaker)
            sb.Append(' ').Append(RichText.Color(MutedColor, "<size=75%>(speaking)</size>"));
        sb.Append('\n');
        sb.Append(RichText.Color(fresh ? "DDDDDD" : "A0A0A0", "<size=85%>" + RichText.Escape(e.Short) + "</size>"));
        return sb.ToString();
    }

    public static string FormatEnglish(LookupResult result)
    {
        var sb = new StringBuilder();
        string headword = result.Definition?.Word is { Length: > 0 } w ? w : result.Query;
        sb.Append("<size=150%><b>").Append(RichText.Escape(headword)).Append("</b></size>\n");
        sb.Append(RichText.Color(Categories.Color(Categories.Vocabulary), "<size=80%>ENGLISH DICTIONARY</size>"));
        if (result.Definition != null && result.Definition.Phonetic.Length > 0)
            sb.Append("   ").Append(RichText.Color(MutedColor, "<size=80%>" + RichText.Escape(result.Definition.Phonetic) + "</size>"));
        sb.Append("\n\n");

        if (result.Status != LookupStatus.Found || result.Definition == null)
        {
            sb.Append(RichText.Color(MutedColor, RichText.Escape(result.Message.Length > 0 ? result.Message : "No definition found.")));
            return sb.ToString();
        }

        string currentPos = "";
        int number = 0;
        foreach (var sense in result.Definition.Senses)
        {
            if (!string.Equals(sense.PartOfSpeech, currentPos, StringComparison.OrdinalIgnoreCase))
            {
                currentPos = sense.PartOfSpeech;
                number = 0;
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                    sb.Append('\n');
                sb.Append(Heading(currentPos.Length > 0 ? currentPos : "meaning")).Append('\n');
            }
            number++;
            sb.Append(number).Append(". ").Append(RichText.Escape(sense.Definition)).Append('\n');
            if (sense.Example.Length > 0)
                sb.Append("    ").Append(RichText.Color(MutedColor, "<i>\"" + RichText.Escape(sense.Example) + "\"</i>")).Append('\n');
        }

        sb.Append('\n').Append(RichText.Color(MutedColor, "<size=75%>Source: dictionaryapi.dev (Wiktionary)</size>"));
        return sb.ToString().TrimEnd();
    }

    /// <summary>Escapes <paramref name="text"/> and turns mentions of other glossary terms into links.</summary>
    public string Linkify(string text, string? excludeId = null)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var sb = new StringBuilder(text.Length + 64);
        int pos = 0;
        var linked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _linkMatcher.FindAll(text))
        {
            if (string.Equals(m.Entry.Id, excludeId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (CanLinkTo != null && !CanLinkTo(m.Entry))
                continue;
            // Only link the first mention of each term, like a wiki does.
            if (!linked.Add(m.Entry.Id))
                continue;

            sb.Append(RichText.Escape(text.Substring(pos, m.Start - pos)));
            sb.Append(EntryLink(m.Entry, m.Text));
            pos = m.End;
        }
        sb.Append(RichText.Escape(text.Substring(pos)));
        return sb.ToString();
    }

    private static string EntryLink(GlossaryEntry target, string shownText) =>
        RichText.Link(EntryLinkPrefix + target.Id, RichText.Color(LinkColor, RichText.Escape(shownText)));

    private static string Heading(string text) => RichText.Color(HeadingColor, "<b>" + RichText.Escape(text) + "</b>");
}
