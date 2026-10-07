using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DiscoDictionary.Core;
using MelonLoader;

namespace DiscoDictionary;

/// <summary>
/// Owns the loaded glossary: the built-in entries embedded in the DLL plus any files the player
/// drops into UserData/DiscoDictionary/glossary. Can be reloaded while the game runs.
/// </summary>
internal sealed class ContentStore
{
    private const string ResourcePrefix = "DiscoDictionary.Glossary.";

    private readonly string _userGlossaryDir;
    private readonly MelonLogger.Instance _log;

    public ContentStore(string dataDir, MelonLogger.Instance log)
    {
        _userGlossaryDir = Path.Combine(dataDir, "glossary");
        _log = log;
        Glossary = new Glossary(Array.Empty<GlossaryEntry>());
        AutoMatcher = new TermMatcher(Array.Empty<GlossaryEntry>());
        LookupMatcher = AutoMatcher;
        Formatter = new EntryFormatter(Glossary, AutoMatcher);
    }

    public Glossary Glossary { get; private set; }

    /// <summary>Recognises terms in dialogue (entries with autoDetect).</summary>
    public TermMatcher AutoMatcher { get; private set; }

    /// <summary>Recognises every entry, for explicit lookups and links.</summary>
    public TermMatcher LookupMatcher { get; private set; }

    public EntryFormatter Formatter { get; private set; }

    public string UserGlossaryDir => _userGlossaryDir;

    public void Reload()
    {
        EnsureUserFolder();

        var sources = new List<KeyValuePair<string, string>>();
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null)
                continue;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            sources.Add(new KeyValuePair<string, string>("built-in " + name.Substring(ResourcePrefix.Length), reader.ReadToEnd()));
        }

        int userFiles = 0;
        foreach (var file in Directory.GetFiles(_userGlossaryDir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                sources.Add(new KeyValuePair<string, string>(Path.GetFileName(file), File.ReadAllText(file, Encoding.UTF8)));
                userFiles++;
            }
            catch (Exception ex)
            {
                _log.Warning($"Could not read {file}: {ex.Message}");
            }
        }

        var result = GlossaryLoader.Load(sources);
        Glossary = result.Glossary;
        AutoMatcher = new TermMatcher(Glossary.Entries);
        LookupMatcher = new TermMatcher(Glossary.Entries, includeManualOnly: true);
        Formatter = new EntryFormatter(Glossary, LookupMatcher);

        _log.Msg($"Loaded {Glossary.Count} dictionary entries ({userFiles} file(s) of your own from {_userGlossaryDir}).");
        foreach (var warning in result.Warnings)
            _log.Warning("Glossary: " + warning);
    }

    private void EnsureUserFolder()
    {
        try
        {
            Directory.CreateDirectory(_userGlossaryDir);
            var example = Path.Combine(_userGlossaryDir, "my-entries.json");
            if (!File.Exists(example))
                File.WriteAllText(example, ExampleFile, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _log.Warning($"Could not create {_userGlossaryDir}: {ex.Message}");
        }
    }

    private const string ExampleFile = @"{
  // Your own dictionary entries. Every .json file in this folder is loaded after the built-in ones.
  // To add an entry, copy the example below out of the comment and fill it in.
  // An entry with the same ""id"" as a built-in one replaces it (ids are listed in the GitHub repo).
  // Press F1 in game and click ""Reload glossary"" at the bottom to see your changes without restarting.
  //
  // {
  //   ""term"": ""Example Word"",
  //   ""aliases"": [""example words""],
  //   ""category"": ""Slang & Jargon"",     // Skills, Game Mechanics, People, Places, World, Factions,
  //                                         // Politics & Ideas, History, Slang & Jargon, Vocabulary
  //   ""short"": ""One line, shown in the sidebar."",
  //   ""details"": ""As much explanation as you like."",
  //   ""inspiredBy"": ""Optional real-world origin."",
  //   ""spoiler"": ""Optional; hidden until clicked."",
  //   ""seeAlso"": [""revachol""],
  //   ""caseSensitive"": false              // true = only match with this exact capitalisation
  // }
  ""entries"": [
  ]
}
";
}
