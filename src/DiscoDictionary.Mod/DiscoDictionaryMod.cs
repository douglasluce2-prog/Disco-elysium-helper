using System;
using System.IO;
using DiscoDictionary.Core;
using DiscoDictionary.UI;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(DiscoDictionary.DiscoDictionaryMod), "Disco Dictionary", "1.0.0", "douglasluce2-prog")]
[assembly: MelonGame("ZAUM Studio", "Disco Elysium")]
[assembly: HarmonyDontPatchAll]

namespace DiscoDictionary;

/// <summary>
/// Entry point. Explains Disco Elysium's words, names, places, politics and skills while you play:
/// a sidebar for terms in the current dialogue, a searchable dictionary window, and a key to look up
/// any word under the mouse (falling back to an online English dictionary for ordinary words).
/// </summary>
public sealed class DiscoDictionaryMod : MelonMod
{
    private const float SaveInterval = 30f;
    private const float UiRetryInterval = 1f;
    private const int MaxLoggedErrors = 5;

    private ModContext? _ctx;
    private DictionaryUi? _ui;
    private float _nextUiAttempt;
    private float _nextSave;
    private int _errors;
    private DialogueLine _lastLine;
    private float _lastLineTime = float.NegativeInfinity;

    public override void OnInitializeMelon()
    {
        string dataDir = Path.Combine(MelonEnvironment.UserDataDirectory, "DiscoDictionary");
        Directory.CreateDirectory(dataDir);

        var settings = new Settings();
        var content = new ContentStore(dataDir, LoggerInstance);
        var seen = new SeenStore(Path.Combine(dataDir, "seen.json"));
        seen.Load();
        var english = new EnglishDictionaryClient(Path.Combine(dataDir, "english-cache.json"));

        _ctx = new ModContext(settings, content, seen, english, LoggerInstance);
        _ctx.ReloadContent();

        DialogueHook.Install(HarmonyInstance, LoggerInstance);

        LoggerInstance.Msg($"Ready. {settings.OpenKeyCode} opens the dictionary, {settings.LookupKeyCode} looks up the word under the mouse. Settings: UserData/MelonPreferences.cfg [DiscoDictionary].");
    }

    public override void OnUpdate()
    {
        if (_ctx == null)
            return;
        try
        {
            Tick(_ctx);
        }
        catch (Exception ex)
        {
            // Log a few errors, then stay quiet rather than flooding the console every frame.
            if (_errors++ < MaxLoggedErrors)
                LoggerInstance.Error("Disco Dictionary hit an error (the game is unaffected): " + ex);
        }
    }

    public override void OnApplicationQuit() => SaveState();

    public override void OnDeinitializeMelon()
    {
        SaveState();
        _ctx?.English.Dispose();
    }

    private void Tick(ModContext ctx)
    {
        float now = Time.unscaledTime;

        if (_ui == null && now >= _nextUiAttempt)
        {
            _nextUiAttempt = now + UiRetryInterval;
            _ui = DictionaryUi.TryCreate(ctx);
        }

        while (DialogueHook.Pending.TryDequeue(out var line))
            HandleLine(ctx, line, now);

        if (_ui == null)
            return;

        var mouse = Input.mousePosition;
        var settings = ctx.Settings;

        if (Input.GetKeyDown(settings.OpenKeyCode))
            _ui.Window.Toggle();
        else if (Input.GetKeyDown(settings.SidebarKeyCode) && !_ui.Window.IsOpen)
            _ui.ToggleSidebar();

        bool lookupPressed = Input.GetKeyDown(settings.LookupKeyCode)
                             || (settings.MiddleClickLookup.Value && Input.GetMouseButtonDown(2));
        if (lookupPressed)
            LookUpUnderMouse(ctx, _ui, mouse);

        _ui.Update(mouse, now);

        if (now >= _nextSave)
        {
            _nextSave = now + SaveInterval;
            SaveState();
        }
    }

    private void HandleLine(ModContext ctx, DialogueLine line, float now)
    {
        // The log can be re-rendered (e.g. when it is reopened); skip immediate repeats.
        if (line.Text == _lastLine.Text && line.Speaker == _lastLine.Speaker && now - _lastLineTime < 1f)
            return;
        _lastLine = line;
        _lastLineTime = now;

        var found = ctx.Analyzer.AddLine(line.Speaker, line.Text, ctx.Settings.ShowSpeakerEntries.Value);
        if (found.Count > 0)
            _ui?.NotifyNewTerms(now);

        if (ctx.Settings.LogDialogue.Value)
        {
            string terms = found.Count == 0 ? "-" : string.Join(", ", found.ConvertAll(e => e.Term));
            LoggerInstance.Msg($"[{line.Speaker}] {RichText.Strip(line.Text)}  =>  {terms}");
        }
    }

    private static void LookUpUnderMouse(ModContext ctx, DictionaryUi ui, Vector3 mouse)
    {
        var picked = WordPicker.Pick(mouse, ui.Root, fromOwnUi: ui.ContainsMouse(mouse));
        if (picked == null)
            return;

        var window = ui.Window;
        var match = ctx.Content.LookupMatcher.FindAt(picked.PlainText, picked.CharIndex);
        if (match != null)
        {
            window.Open();
            window.ShowEntry(match.Entry);
            return;
        }

        string word = Tokenizer.WordAt(picked.PlainText, picked.CharIndex) ?? picked.Word;

        // A case-insensitive name typed differently (e.g. "rcm") still counts; a case-sensitive one
        // ("pale" vs "the Pale") deliberately doesn't, so ordinary words get their English meaning.
        var byName = ctx.Content.Glossary.FindByName(word);
        window.Open();
        window.SetQuery(word);
        if (byName != null && !byName.CaseSensitive && ctx.IsVisible(byName))
            window.ShowEntry(byName);
        else
            window.ShowEnglish(word);
    }

    private void SaveState()
    {
        if (_ctx == null)
            return;
        try
        {
            _ctx.Seen.Save();
            _ctx.English.SaveCache();
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning("Could not save dictionary data: " + ex.Message);
        }
    }
}
