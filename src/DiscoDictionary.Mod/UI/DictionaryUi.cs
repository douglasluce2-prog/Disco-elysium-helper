using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;

namespace DiscoDictionary.UI;

/// <summary>Creates and drives the sidebar and the dictionary window.</summary>
internal sealed class DictionaryUi
{
    private readonly ModContext _ctx;
    private readonly GameObject _canvas;
    private bool _sidebarHiddenByPlayer;
    private float _lastNewTerms = float.NegativeInfinity;

    private DictionaryUi(ModContext ctx, TMP_FontAsset font)
    {
        _ctx = ctx;
        _canvas = UiKit.CreateCanvas("DiscoDictionary.Canvas", ctx.Settings.Scale);

        var s = ctx.Settings;
        string hint = $"{s.OpenKeyCode} open  |  {s.SidebarKeyCode} hide  |  {s.LookupKeyCode} look up";
        Sidebar = new SidebarView(_canvas.transform, font, s.SidebarOnRight.Value, hint);
        Window = new DictionaryWindow(_canvas.transform, font, ctx);
        Sidebar.SetVisible(false);

        ctx.Reloaded += Window.OnContentReloaded;
    }

    public SidebarView Sidebar { get; }

    public DictionaryWindow Window { get; }

    public Transform Root => _canvas.transform;

    /// <summary>
    /// Builds the UI once the game has loaded a TextMeshPro font we can borrow. Returns null if
    /// there isn't one yet (very early in startup); the caller simply tries again a bit later.
    /// </summary>
    public static DictionaryUi? TryCreate(ModContext ctx)
    {
        var font = FindFont(ctx);
        return font == null ? null : new DictionaryUi(ctx, font);
    }

    public bool ContainsMouse(Vector3 mouse) => Window.ContainsMouse(mouse) || Sidebar.ContainsMouse(mouse);

    public void NotifyNewTerms(float now) => _lastNewTerms = now;

    public void ToggleSidebar()
    {
        if (Sidebar.Visible)
        {
            _sidebarHiddenByPlayer = true;
            return;
        }

        // Showing it again should really show it, even if it had timed out or was switched off.
        _sidebarHiddenByPlayer = false;
        _lastNewTerms = Time.unscaledTime;
        if (!_ctx.Settings.ShowSidebar.Value)
        {
            _ctx.Settings.ShowSidebar.Value = true;
            _ctx.Settings.Save();
        }
    }

    public void Update(Vector3 mouse, float now)
    {
        Window.Update(mouse, now);

        var s = _ctx.Settings;
        int hideAfter = s.SidebarHideAfterSeconds.Value;
        bool recentEnough = hideAfter <= 0 || now - _lastNewTerms < hideAfter;
        bool show = s.ShowSidebar.Value
                    && !_sidebarHiddenByPlayer
                    && !Window.IsOpen
                    && _ctx.Analyzer.Recent.Count > 0
                    && recentEnough;
        Sidebar.SetVisible(show);
        if (!show)
            return;

        Sidebar.Render(_ctx.Analyzer);
        if (Input.GetMouseButtonDown(0))
        {
            var link = Sidebar.LinkAt(mouse);
            if (link != null)
                Window.Follow(link);
        }
    }

    private static TMP_FontAsset? FindFont(ModContext ctx)
    {
        var fonts = new List<TMP_FontAsset>();
        foreach (var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TMP_FontAsset>()))
        {
            var font = obj?.TryCast<TMP_FontAsset>();
            if (font != null)
                fonts.Add(font);
        }
        if (fonts.Count == 0)
            return null;

        string wanted = ctx.Settings.FontName.Value?.Trim() ?? "";
        if (wanted.Length > 0)
        {
            var named = fonts.FirstOrDefault(f => f.name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0);
            if (named != null)
            {
                ctx.Log.Msg($"Using font '{named.name}'.");
                return named;
            }
            ctx.Log.Warning($"No font matching FontName '{wanted}'.");
        }

        // Otherwise use whichever font the game's own on-screen texts use most.
        var usage = new Dictionary<string, int>();
        TMP_FontAsset? best = null;
        int bestCount = 0;
        foreach (var obj in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<TextMeshProUGUI>()))
        {
            var text = obj?.TryCast<TextMeshProUGUI>();
            var font = text?.font;
            if (font == null)
                continue;
            usage.TryGetValue(font.name, out int count);
            usage[font.name] = ++count;
            if (count > bestCount)
            {
                bestCount = count;
                best = font;
            }
        }

        if (best == null)
            return null; // No UI text on screen yet; try again shortly.

        ctx.Log.Msg($"Using the game's font '{best.name}'. Available fonts (for the FontName setting): {string.Join(", ", fonts.Select(f => f.name).Distinct())}");
        return best;
    }
}
