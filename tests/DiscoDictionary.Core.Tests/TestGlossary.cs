using System.Collections.Generic;
using DiscoDictionary.Core;

namespace DiscoDictionary.Core.Tests;

/// <summary>A small hand-made glossary so matcher tests don't depend on the real content.</summary>
internal static class TestGlossary
{
    public const string Json = @"{
  ""category"": ""World"",
  ""entries"": [
    { ""term"": ""Revachol"", ""aliases"": [""Revacholian""], ""category"": ""Places"", ""short"": ""The city."" },
    { ""id"": ""rcm"", ""term"": ""Revachol Citizens Militia"", ""aliases"": [""RCM""], ""category"": ""Factions"", ""short"": ""The police."" },
    { ""term"": ""Pale"", ""caseSensitive"": true, ""commonWord"": true, ""short"": ""The grey void between landmasses."", ""seeAlso"": [""isola""] },
    { ""term"": ""Isola"", ""short"": ""A landmass surrounded by Pale."" },
    { ""term"": ""Inland Empire"", ""category"": ""Skills"", ""caseSensitive"": true, ""short"": ""Imagination and hunches."" },
    { ""term"": ""Whirling-in-Rags"", ""aliases"": [""Whirling""], ""category"": ""Places"", ""short"": ""The hostel."" },
    { ""term"": ""Hardie Boys"", ""category"": ""Factions"", ""short"": ""Dockworker vigilantes."" },
    { ""term"": ""réal"", ""category"": ""World"", ""short"": ""Currency."" },
    { ""term"": ""Esprit de Corps"", ""category"": ""Skills"", ""short"": ""Cop camaraderie."" },
    { ""term"": ""Secret Name"", ""short"": ""A spoiler name."", ""hideUntilSeen"": true, ""spoiler"": ""It was them all along."" },
    { ""term"": ""Manual"", ""short"": ""Only reachable by search."", ""autoDetect"": false }
  ]
}";

    public static Glossary Load(out List<string> warnings)
    {
        var result = GlossaryLoader.Load(new[] { new KeyValuePair<string, string>("test.json", Json) });
        warnings = result.Warnings;
        return result.Glossary;
    }

    public static Glossary Load() => Load(out _);
}
