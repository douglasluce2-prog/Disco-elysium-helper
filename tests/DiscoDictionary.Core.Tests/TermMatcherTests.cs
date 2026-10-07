using System.Linq;
using DiscoDictionary.Core;
using Xunit;

namespace DiscoDictionary.Core.Tests;

public class TermMatcherTests
{
    private static readonly Glossary G = TestGlossary.Load();
    private static readonly TermMatcher Matcher = new(G.Entries);

    private static string[] Ids(string text, bool ignoreCase = false) =>
        Matcher.FindAll(text, ignoreCase).Select(m => m.Entry.Id).ToArray();

    [Fact]
    public void FindsSimpleTerm()
    {
        var matches = Matcher.FindAll("Welcome to Revachol, detective.");
        var m = Assert.Single(matches);
        Assert.Equal("revachol", m.Entry.Id);
        Assert.Equal("Revachol", m.Text);
        Assert.Equal(11, m.Start);
    }

    [Fact]
    public void LongestPhraseWins()
    {
        Assert.Equal(new[] { "rcm" }, Ids("He's with the Revachol Citizens Militia."));
    }

    [Fact]
    public void PhraseDoesNotCrossPunctuation()
    {
        Assert.Equal(new[] { "revachol" }, Ids("This is Revachol. Citizens Militia is elsewhere."));
    }

    [Fact]
    public void UnderstandsPossessivesAndPlurals()
    {
        Assert.Equal(new[] { "revachol" }, Ids("Revachol's harbour"));
        Assert.Equal(new[] { "revachol" }, Ids("Revachol’s harbour"));
        Assert.Equal(new[] { "revachol" }, Ids("two Revacholians"));
        Assert.Equal(new[] { "isola" }, Ids("the isolas"));
        Assert.Equal(new[] { "hardie-boys" }, Ids("the Hardie Boys' bar"));
    }

    [Fact]
    public void CaseSensitiveEntriesIgnoreOrdinaryWords()
    {
        Assert.Empty(Ids("His face went pale."));
        Assert.Equal(new[] { "pale" }, Ids("Beyond lies the Pale."));
        Assert.Equal(new[] { "pale" }, Ids("THE PALE"));
        Assert.Empty(Ids("an inland empire of trade"));
        Assert.Equal(new[] { "inland-empire" }, Ids("INLAND EMPIRE - It whispers."));
    }

    [Fact]
    public void CommonWordsAreIgnoredAtSentenceStart()
    {
        Assert.Empty(Ids("Pale light filled the room."));
        Assert.Empty(Ids("He stopped. \"Pale as a ghost,\" she said."));
        Assert.Equal(new[] { "pale" }, Ids("They fear the Pale."));
        Assert.Equal(new[] { "pale" }, Ids("PALE LIGHT"));

        // Pointing at the word on purpose still finds it.
        Assert.Equal("pale", Matcher.FindAt("Pale light filled the room.", 1)!.Entry.Id);
    }

    [Fact]
    public void IgnoreCaseForSpeakerNames()
    {
        Assert.Equal(new[] { "inland-empire" }, Ids("inland empire", ignoreCase: true));
    }

    [Fact]
    public void HyphenatedAndMultiWordTerms()
    {
        Assert.Equal(new[] { "whirling-in-rags" }, Ids("Back at the Whirling-in-Rags."));
        Assert.Equal(new[] { "whirling-in-rags" }, Ids("Back at the Whirling in Rags."));
        Assert.Equal(new[] { "whirling-in-rags" }, Ids("the Whirling."));
        Assert.Equal(new[] { "esprit-de-corps" }, Ids("[Esprit de Corps - Medium 10]"));
    }

    [Fact]
    public void AccentsMatter()
    {
        Assert.Equal(new[] { "real" }, Ids("It costs 20 réal."));
        Assert.Empty(Ids("Is this real?"));
    }

    [Fact]
    public void IgnoresManualOnlyEntriesUnlessAsked()
    {
        Assert.Empty(Ids("The Manual says so."));
        var all = new TermMatcher(G.Entries, includeManualOnly: true);
        Assert.Single(all.FindAll("The Manual says so."));
    }

    [Fact]
    public void FindsSeveralTermsInOrder()
    {
        Assert.Equal(new[] { "rcm", "pale", "revachol" }, Ids("The RCM fears the Pale more than Revachol does."));
    }

    [Fact]
    public void FindAtLocatesPhraseUnderCursor()
    {
        const string text = "Your INLAND EMPIRE stirs.";
        int idx = text.IndexOf("EMPIRE", System.StringComparison.Ordinal) + 2;
        var m = Matcher.FindAt(text, idx);
        Assert.NotNull(m);
        Assert.Equal("inland-empire", m!.Entry.Id);

        Assert.Null(Matcher.FindAt(text, text.IndexOf("stirs", System.StringComparison.Ordinal)));
        Assert.Null(Matcher.FindAt(text, 999));
    }

    [Fact]
    public void FindEntriesIsDistinct()
    {
        var entries = Matcher.FindEntries("Revachol, Revachol, always Revachol.");
        Assert.Single(entries);
    }

    [Fact]
    public void WordAtFindsTheWordUnderACharacter()
    {
        const string text = "The Hardie Boys' bar, in Revachol's west.";
        Assert.Equal("Boys", Tokenizer.WordAt(text, text.IndexOf("Boys", System.StringComparison.Ordinal) + 1));
        Assert.Equal("Revachol", Tokenizer.WordAt(text, text.IndexOf("Revachol", System.StringComparison.Ordinal)));
        Assert.Null(Tokenizer.WordAt(text, text.IndexOf(',')));
        Assert.Null(Tokenizer.WordAt(text, -1));
    }

    [Fact]
    public void EmptyInputIsFine()
    {
        Assert.Empty(Matcher.FindAll(null));
        Assert.Empty(Matcher.FindAll(""));
        Assert.Null(Matcher.FindAt(null, 0));
    }
}
