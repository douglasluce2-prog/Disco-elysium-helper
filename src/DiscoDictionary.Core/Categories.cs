using System;
using System.Collections.Generic;

namespace DiscoDictionary.Core;

/// <summary>The fixed set of categories, in the order they are shown in the dictionary window.</summary>
public static class Categories
{
    public const string Skills = "Skills";
    public const string Mechanics = "Game Mechanics";
    public const string People = "People";
    public const string Places = "Places";
    public const string World = "World";
    public const string Factions = "Factions";
    public const string Politics = "Politics & Ideas";
    public const string History = "History";
    public const string Slang = "Slang & Jargon";
    public const string Vocabulary = "Vocabulary";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Skills, Mechanics, People, Places, World, Factions, Politics, History, Slang, Vocabulary,
    };

    public static bool IsKnown(string category)
    {
        var canonical = Canonical(category);
        foreach (var c in All)
        {
            if (string.Equals(c, canonical, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the canonical spelling of a category, or the input unchanged if unknown.
    /// Short forms are accepted for hand-written files: "slang", "politics", "mechanics".
    /// </summary>
    public static string Canonical(string category)
    {
        foreach (var c in All)
        {
            if (string.Equals(c, category, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        foreach (var c in All)
        {
            foreach (var word in c.Split(' '))
            {
                if (word.Length > 2 && string.Equals(word, category, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
        }
        return category;
    }

    /// <summary>Accent colour per category (hex, no '#'), used by the formatter.</summary>
    public static string Color(string category) => Canonical(category) switch
    {
        Skills => "8FB8DE",
        Mechanics => "A0C49D",
        People => "E3B46B",
        Places => "C79BD8",
        World => "7FC7C0",
        Factions => "E08D6B",
        Politics => "E06C75",
        History => "C9A66B",
        Slang => "D4C66A",
        Vocabulary => "B8B8B8",
        _ => "CCCCCC",
    };
}
