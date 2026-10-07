using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscoDictionary.Core;
using Xunit;
using Xunit.Abstractions;

namespace DiscoDictionary.Core.Tests;

/// <summary>
/// Checks the real glossary files in data/glossary. If you edit or add entries, run
/// "dotnet test" and these will tell you about typos in ids, broken "seeAlso" links,
/// two entries claiming the same name, and so on.
/// </summary>
public class GlossaryDataTests
{
    private readonly ITestOutputHelper _output;

    public GlossaryDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static List<KeyValuePair<string, string>> Files()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "glossary");
        return Directory.GetFiles(dir, "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new KeyValuePair<string, string>(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();
    }

    private static GlossaryLoadResult LoadReal() => GlossaryLoader.Load(Files());

    [Fact]
    public void LoadsWithoutWarnings()
    {
        var result = LoadReal();
        foreach (var w in result.Warnings)
            _output.WriteLine(w);
        Assert.Empty(result.Warnings);
        Assert.True(result.Glossary.Count >= 100, $"only {result.Glossary.Count} entries");
    }

    [Fact]
    public void EveryCategoryHasEntries()
    {
        var g = LoadReal().Glossary;
        foreach (var category in Categories.All)
            Assert.Contains(g.Entries, e => e.Category == category);
    }

    [Fact]
    public void TextIsSafeForTheGameUi()
    {
        foreach (var e in LoadReal().Glossary.Entries)
        {
            foreach (var text in new[] { e.Term, e.Short, e.Details, e.InspiredBy, e.Spoiler }.Concat(e.Aliases))
            {
                // '<' would be read as markup by TextMeshPro; tabs render badly.
                Assert.False(text.Contains('<'), $"{e.Id}: contains '<'");
                Assert.False(text.Contains('\t'), $"{e.Id}: contains a tab");
                Assert.False(text.Contains('\u2014'), $"{e.Id}: contains an em dash (may be missing from the game font)");
            }
            Assert.True(e.Short.Length <= GlossaryLoader.MaxShortLength, $"{e.Id}: short is too long");
            Assert.True(char.IsUpper(e.Short[0]) || char.IsDigit(e.Short[0]) || e.Short[0] == '\'' || e.Short[0] == '"', $"{e.Id}: short should start with a capital");
        }
    }

    [Fact]
    public void SpoilerNamesAreHiddenAndHaveSafeShorts()
    {
        var g = LoadReal().Glossary;
        foreach (var e in g.Entries.Where(e => e.HideUntilSeen))
            Assert.False(string.IsNullOrWhiteSpace(e.Short), e.Id);

        // A few names that would spoil the story if browsable from the start.
        foreach (var id in new[] { "iosef-dros", "lely", "dora", "harry" })
            Assert.True(g.Get(id)!.HideUntilSeen, id);
    }

    [Theory]
    [InlineData("KIM KITSURAGI", true, "kim-kitsuragi")]
    [InlineData("INLAND EMPIRE", true, "inland-empire")]
    [InlineData("HAND/EYE COORDINATION", true, "hand-eye-coordination")]
    [InlineData("[Esprit de Corps - Challenging 12]", false, "esprit-de-corps")]
    [InlineData("[Logic - Medium 10]", false, "logic")]
    [InlineData("The Coalition crushed the Commune.", false, "coalition", "commune")]
    [InlineData("Twenty réal for the room, officer.", false, "real")]
    [InlineData("You're RCM? Precinct 41?", false, "rcm", "precinct-41")]
    [InlineData("The Hardie Boys' story doesn't add up.", false, "hardie-boys")]
    [InlineData("Lena says the Insulindian Phasmid is real.", false, "lena", "insulindian-phasmid")]
    [InlineData("A mazovian? Here? In this economy?", false)]
    [InlineData("The Pale is out there, past the isolas.", false, "pale", "isola")]
    [InlineData("You walk back to the Whirling-in-Rags.", false, "whirling-in-rags")]
    public void RecognisesTermsInTypicalLines(string text, bool isSpeaker, params string[] expectedIds)
    {
        var matcher = new TermMatcher(LoadReal().Glossary.Entries);
        var ids = matcher.FindEntries(text, ignoreCase: isSpeaker).Select(e => e.Id).ToArray();
        Assert.Equal(expectedIds, ids);
    }

    [Theory]
    [InlineData("His face is pale and his hands shake.")]
    [InlineData("That's just logic, and a bit of drama.")]
    [InlineData("Let me check the health of the patient; her morale is low.")]
    [InlineData("The union of two rivers; a coalition of the willing.")]
    [InlineData("It is a real problem with real consequences.")]
    [InlineData("Endurance and composure are virtues, said the volition-less man.")]
    [InlineData("I have a thought about suggestion and perception.")]
    public void IgnoresOrdinaryEnglish(string text)
    {
        var matcher = new TermMatcher(LoadReal().Glossary.Entries);
        var found = matcher.FindEntries(text).Select(e => e.Id).ToArray();
        Assert.True(found.Length == 0, "false positives: " + string.Join(", ", found));
    }

    [Fact]
    public void EveryNameIsRecognisedAsItself()
    {
        var g = LoadReal().Glossary;
        var matcher = new TermMatcher(g.Entries, includeManualOnly: true);
        foreach (var e in g.Entries)
        {
            foreach (var name in e.AllNames())
            {
                var m = matcher.FindAt(name, 0);
                Assert.True(m != null && m.Entry == e, $"'{name}' should resolve to {e.Id} but got {m?.Entry.Id ?? "nothing"}");
            }
        }
    }
}
