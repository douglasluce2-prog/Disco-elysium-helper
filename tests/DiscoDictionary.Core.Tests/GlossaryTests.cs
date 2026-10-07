using System.Collections.Generic;
using System.Linq;
using DiscoDictionary.Core;
using Xunit;

namespace DiscoDictionary.Core.Tests;

public class GlossaryTests
{
    [Fact]
    public void LoaderDerivesIdsAndInheritsCategory()
    {
        var g = TestGlossary.Load(out var warnings);
        Assert.Empty(warnings);
        Assert.Equal("Places", g.Get("revachol")!.Category);
        Assert.Equal("World", g.Get("isola")!.Category);
        Assert.Equal("whirling-in-rags", g.FindByName("Whirling-in-Rags")!.Id);
        Assert.Equal("real", g.FindByName("réal")!.Id);
    }

    [Fact]
    public void FindByNameIgnoresCaseAndAccents()
    {
        var g = TestGlossary.Load();
        Assert.Equal("real", g.FindByName("REAL")!.Id);
        Assert.Equal("rcm", g.FindByName("rcm")!.Id);
        Assert.Null(g.FindByName("nothing"));
    }

    [Fact]
    public void SearchRanksNameMatchesFirst()
    {
        var g = TestGlossary.Load();
        var results = g.Search("pale");
        Assert.Equal("pale", results[0].Id);
        // "Isola" mentions Pale in its definition, so it is found too, but lower.
        Assert.Contains(results, e => e.Id == "isola");
    }

    [Fact]
    public void SearchByWordPrefixAndCategory()
    {
        var g = TestGlossary.Load();
        Assert.Equal("inland-empire", g.Search("emp").First().Id);
        var skills = g.Search("", category: "Skills");
        Assert.Equal(new[] { "esprit-de-corps", "inland-empire" }, skills.Select(e => e.Id).ToArray());
    }

    [Fact]
    public void SearchHonoursVisibilityFilter()
    {
        var g = TestGlossary.Load();
        var visible = g.Search("secret", e => !e.HideUntilSeen);
        Assert.Empty(visible);
        Assert.Single(g.Search("secret"));
    }

    [Fact]
    public void LaterSourcesOverrideEarlierOnes()
    {
        var user = @"{ ""entries"": [ { ""id"": ""revachol"", ""term"": ""Revachol"", ""category"": ""Places"", ""short"": ""My own note."" },
                                     { ""term"": ""Brand New"", ""category"": ""Slang"", ""short"": ""Added by me."" } ] }";
        var result = GlossaryLoader.Load(new[]
        {
            new KeyValuePair<string, string>("builtin.json", TestGlossary.Json),
            new KeyValuePair<string, string>("user.json", user),
        });
        Assert.Equal("My own note.", result.Glossary.Get("revachol")!.Short);
        Assert.NotNull(result.Glossary.Get("brand-new"));
        Assert.Equal("Slang & Jargon", result.Glossary.Get("brand-new")!.Category);
    }

    [Fact]
    public void BadDataProducesWarningsNotExceptions()
    {
        var result = GlossaryLoader.Load(new[]
        {
            new KeyValuePair<string, string>("broken.json", "{ not json"),
            new KeyValuePair<string, string>("odd.json", @"{ ""entries"": [
                { ""short"": ""no term"" },
                { ""term"": ""A"", ""category"": ""Nonsense"", ""short"": ""x"", ""seeAlso"": [""missing""] },
                { ""term"": ""B"", ""aliases"": [""A""], ""category"": ""World"", ""short"": ""y"" }
            ] }"),
        });

        Assert.Equal(2, result.Glossary.Count);
        Assert.Contains(result.Warnings, w => w.Contains("broken.json"));
        Assert.Contains(result.Warnings, w => w.Contains("no \"term\""));
        Assert.Contains(result.Warnings, w => w.Contains("unknown category"));
        Assert.Contains(result.Warnings, w => w.Contains("'missing'"));
        Assert.Contains(result.Warnings, w => w.Contains("name of both"));
    }

    [Fact]
    public void JsonCommentsAndTrailingCommasAreAllowed()
    {
        var json = @"{
            // players will hand-edit these files
            ""entries"": [ { ""term"": ""X"", ""category"": ""World"", ""short"": ""y"", }, ],
        }";
        var result = GlossaryLoader.Load(new[] { new KeyValuePair<string, string>("c.json", json) });
        Assert.Empty(result.Warnings);
        Assert.Equal(1, result.Glossary.Count);
    }

    [Fact]
    public void NormalizeFoldsAccentsCaseAndPunctuation()
    {
        Assert.Equal("whirling in rags", Glossary.Normalize("Whirling-in-Rags"));
        Assert.Equal("real", Glossary.Normalize("Réal"));
        Assert.Equal("revachols harbour", Glossary.Normalize("  Revachol's   harbour! "));
        Assert.Equal("rene arnoux", Glossary.Normalize("Ren\u00E9 Arnoux"));
        Assert.Equal("petanque deja vu", Glossary.Normalize("P\u00E9tanque, D\u00C9J\u00C0 VU"));
    }

    [Fact]
    public void IdsAreAccentFreeWithoutUnicodeNormalization()
    {
        Assert.Equal("rene-arnoux", GlossaryLoader.MakeId("Ren\u00E9 Arnoux"));
        Assert.Equal('E', Glossary.FoldAccent('\u00C9'));
        Assert.Equal('l', Glossary.FoldAccent('\u0142'));
    }
}
