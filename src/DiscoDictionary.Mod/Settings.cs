using System;
using MelonLoader;
using UnityEngine;

namespace DiscoDictionary;

/// <summary>
/// Player options. They live in UserData/MelonPreferences.cfg under [DiscoDictionary]
/// and can be edited with any text editor while the game is closed.
/// </summary>
internal sealed class Settings
{
    private readonly MelonPreferences_Category _category;

    public Settings()
    {
        _category = MelonPreferences.CreateCategory("DiscoDictionary", "Disco Dictionary");

        OpenKey = Entry("OpenDictionaryKey", "F1",
            "Key that opens and closes the dictionary window. Use a Unity KeyCode name, e.g. F1, F9, BackQuote, Insert.");
        SidebarKey = Entry("ToggleSidebarKey", "F2",
            "Key that shows or hides the 'terms in recent lines' sidebar.");
        LookupKey = Entry("LookupWordKey", "F3",
            "Point the mouse at any word on screen and press this key to look it up.");
        MiddleClickLookup = Entry("MiddleClickLookup", true,
            "Also look up the word under the mouse when you click the middle mouse button.");

        ShowSidebar = Entry("ShowSidebar", true,
            "Show the sidebar that explains terms from the dialogue as you read.");
        SidebarOnRight = Entry("SidebarOnRight", false,
            "Put the sidebar on the right side of the screen instead of the left. (Restart the game to apply.)");
        SidebarMaxItems = Entry("SidebarMaxItems", 6,
            "How many recent terms the sidebar shows (1-15).");
        SidebarHideAfterSeconds = Entry("SidebarHideAfterSeconds", 120,
            "Hide the sidebar when no new terms have appeared for this many seconds. 0 = never hide.");
        ShowSpeakerEntries = Entry("ShowSpeakerEntries", true,
            "Also explain who is speaking (a skill, or a character the dictionary knows).");

        OnlineEnglishLookup = Entry("OnlineEnglishLookup", true,
            "Look up ordinary English words with the free online dictionary at dictionaryapi.dev. Words are cached after the first lookup.");
        RevealSpoilers = Entry("RevealSpoilers", false,
            "Show spoiler sections without having to click them.");
        ShowUnseenEntries = Entry("ShowUnseenEntries", false,
            "List spoiler-sensitive entries (like certain character names) even before you've met them in the game.");

        UiScale = Entry("UiScale", 1.0f,
            "Size of the dictionary's text and panels. 1.0 is normal, 1.25 is 25% bigger. (Restart the game to apply.)");
        FontName = Entry("FontName", "",
            "Leave empty to use the game's own font automatically. Otherwise part of a font name listed in the MelonLoader log.");
        LogDialogue = Entry("LogDialogue", false,
            "Write every dialogue line and the terms found in it to the MelonLoader console (for troubleshooting).");

        _category.SaveToFile(false);
    }

    public MelonPreferences_Entry<string> OpenKey { get; }
    public MelonPreferences_Entry<string> SidebarKey { get; }
    public MelonPreferences_Entry<string> LookupKey { get; }
    public MelonPreferences_Entry<bool> MiddleClickLookup { get; }
    public MelonPreferences_Entry<bool> ShowSidebar { get; }
    public MelonPreferences_Entry<bool> SidebarOnRight { get; }
    public MelonPreferences_Entry<int> SidebarMaxItems { get; }
    public MelonPreferences_Entry<int> SidebarHideAfterSeconds { get; }
    public MelonPreferences_Entry<bool> ShowSpeakerEntries { get; }
    public MelonPreferences_Entry<bool> OnlineEnglishLookup { get; }
    public MelonPreferences_Entry<bool> RevealSpoilers { get; }
    public MelonPreferences_Entry<bool> ShowUnseenEntries { get; }
    public MelonPreferences_Entry<float> UiScale { get; }
    public MelonPreferences_Entry<string> FontName { get; }
    public MelonPreferences_Entry<bool> LogDialogue { get; }

    public KeyCode OpenKeyCode => ParseKey(OpenKey.Value, KeyCode.F1);
    public KeyCode SidebarKeyCode => ParseKey(SidebarKey.Value, KeyCode.F2);
    public KeyCode LookupKeyCode => ParseKey(LookupKey.Value, KeyCode.F3);

    public int SidebarItems => Math.Max(1, Math.Min(15, SidebarMaxItems.Value));

    public float Scale => Math.Max(0.5f, Math.Min(3f, UiScale.Value));

    public void Save() => _category.SaveToFile(false);

    private MelonPreferences_Entry<T> Entry<T>(string id, T defaultValue, string description) =>
        _category.CreateEntry(id, defaultValue, id, description);

    private static KeyCode ParseKey(string? value, KeyCode fallback) =>
        !string.IsNullOrWhiteSpace(value) && Enum.TryParse<KeyCode>(value!.Trim(), true, out var key) ? key : fallback;
}
