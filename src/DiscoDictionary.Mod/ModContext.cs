using System;
using DiscoDictionary.Core;
using MelonLoader;

namespace DiscoDictionary;

/// <summary>Everything the UI needs from the rest of the mod, in one place.</summary>
internal sealed class ModContext
{
    public ModContext(Settings settings, ContentStore content, SeenStore seen, EnglishDictionaryClient english, MelonLogger.Instance log)
    {
        Settings = settings;
        Content = content;
        Seen = seen;
        English = english;
        Log = log;
        Analyzer = new LineAnalyzer(content.AutoMatcher, seen, settings.SidebarItems);
    }

    public Settings Settings { get; }
    public ContentStore Content { get; }
    public SeenStore Seen { get; }
    public EnglishDictionaryClient English { get; }
    public MelonLogger.Instance Log { get; }
    public LineAnalyzer Analyzer { get; private set; }

    /// <summary>Raised after the glossary files were reloaded.</summary>
    public event Action? Reloaded;

    /// <summary>Spoiler-sensitive entries stay out of lists and links until the player has met them.</summary>
    public bool IsVisible(GlossaryEntry entry) =>
        !entry.HideUntilSeen || Seen.IsSeen(entry.Id) || Settings.ShowUnseenEntries.Value;

    public void ReloadContent()
    {
        Content.Reload();
        Content.Formatter.CanLinkTo = IsVisible;
        Analyzer = new LineAnalyzer(Content.AutoMatcher, Seen, Settings.SidebarItems);
        Reloaded?.Invoke();
    }
}
