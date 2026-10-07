using System.IO;
using System.Linq;
using DiscoDictionary.Core;
using Xunit;

namespace DiscoDictionary.Core.Tests;

public class FormatterAndAnalyzerTests
{
    private static readonly Glossary G = TestGlossary.Load();

    [Fact]
    public void RichTextStripRemovesTags()
    {
        Assert.Equal("You are in Revachol.", RichText.Strip("<i>You</i> are in <color=#ff0000>Revachol</color>."));
        Assert.Equal("", RichText.Strip(null));
    }

    [Fact]
    public void EscapeProtectsAngleBrackets()
    {
        Assert.Equal("plain", RichText.Escape("plain"));
        Assert.Equal("<noparse>a <b> c</noparse>", RichText.Escape("a <b> c"));
    }

    [Fact]
    public void LinkifyLinksOtherTermsOnce()
    {
        var formatter = new EntryFormatter(G, new TermMatcher(G.Entries, includeManualOnly: true));
        var text = formatter.Linkify("The Pale surrounds every isola. The Pale is grey.", excludeId: "isola");
        Assert.Contains("<link=\"entry:pale\">", text);
        Assert.DoesNotContain("entry:isola", text);
        Assert.Equal(1, CountOf(text, "entry:pale"));
    }

    [Fact]
    public void LinkifyRespectsCanLinkTo()
    {
        var formatter = new EntryFormatter(G, new TermMatcher(G.Entries)) { CanLinkTo = e => !e.HideUntilSeen };
        var text = formatter.Linkify("Remember the Secret Name.");
        Assert.DoesNotContain("<link", text);
    }

    [Fact]
    public void FormatEntryHidesSpoilerUntilRevealed()
    {
        var formatter = new EntryFormatter(G, new TermMatcher(G.Entries));
        var e = G.Get("secret-name")!;
        var hidden = formatter.FormatEntry(e, spoilerRevealed: false);
        Assert.DoesNotContain("all along", hidden);
        Assert.Contains("spoiler:secret-name", hidden);

        var shown = formatter.FormatEntry(e, spoilerRevealed: true);
        Assert.Contains("It was them all along.", shown);
    }

    [Fact]
    public void FormatEntryIncludesSeeAlsoLinks()
    {
        var formatter = new EntryFormatter(G, new TermMatcher(G.Entries));
        var text = formatter.FormatEntry(G.Get("pale")!, false);
        Assert.Contains("See also", text);
        Assert.Contains("entry:isola", text);
    }

    [Fact]
    public void AnalyzerTracksRecentTermsNewestFirst()
    {
        var seen = new SeenStore();
        var analyzer = new LineAnalyzer(new TermMatcher(G.Entries), seen, capacity: 3);

        analyzer.AddLine("KIM KITSURAGI", "We are in Revachol.");
        analyzer.AddLine("INLAND EMPIRE", "The Pale is coming.");

        Assert.Equal(new[] { "inland-empire", "pale", "revachol" }, analyzer.Recent.Select(r => r.Entry.Id).ToArray());
        Assert.True(analyzer.Recent[0].IsSpeaker);
        Assert.False(analyzer.Recent[1].IsSpeaker);
        Assert.True(seen.IsSeen("pale"));

        // Re-mentioning moves an entry back to the top; capacity is enforced.
        analyzer.AddLine("You", "Back to Revachol, and the Whirling.");
        Assert.Equal(new[] { "revachol", "whirling-in-rags", "inland-empire" }, analyzer.Recent.Select(r => r.Entry.Id).ToArray());
    }

    [Fact]
    public void AnalyzerIgnoresGenericSpeakers()
    {
        var analyzer = new LineAnalyzer(new TermMatcher(G.Entries), new SeenStore());
        var found = analyzer.AddLine("You", "Nothing to see.");
        Assert.Empty(found);
        Assert.Equal(0, analyzer.Version);
    }

    [Fact]
    public void SeenStoreRoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), "dd-seen-" + System.Guid.NewGuid() + ".json");
        try
        {
            var a = new SeenStore(path);
            Assert.True(a.MarkSeen("pale"));
            Assert.False(a.MarkSeen("pale"));
            a.Save();

            var b = new SeenStore(path);
            b.Load();
            Assert.True(b.IsSeen("PALE"));
            Assert.False(b.IsDirty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static int CountOf(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }
        return count;
    }
}
