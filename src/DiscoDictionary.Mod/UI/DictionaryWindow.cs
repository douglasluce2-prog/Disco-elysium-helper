using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscoDictionary.Core;
using Il2CppTMPro;
using UnityEngine;

namespace DiscoDictionary.UI;

/// <summary>
/// The full dictionary: a search box, category filters, a list of entries on the left and the
/// selected entry (or English definition) on the right. Everything clickable is a TextMeshPro link.
/// </summary>
internal sealed class DictionaryWindow
{
    private const float Width = 1380f;
    private const float Height = 840f;
    private const float Pad = 22f;
    private const float ListWidth = 360f;
    private const float BodyTop = 162f;
    private const float FooterHeight = 30f;
    private const float ListFontSize = 19f;
    private const float DetailFontSize = 20f;
    private const float InnerPad = 14f;
    private const float WheelStep = 80f;
    private const int MaxQueryLength = 40;

    private const string LinkClose = "close";
    private const string LinkBack = "back";
    private const string LinkLookup = "lookup";
    private const string LinkReload = "reload";
    private const string LinkAll = "filter:all";
    private const string LinkSeen = "filter:seen";
    private const string LinkCategory = "filter:cat:";
    private const string LinkMore = "more";

    private readonly ModContext _ctx;
    private readonly RectTransform _root;
    private readonly TextMeshProUGUI _title;
    private readonly TextMeshProUGUI _close;
    private readonly TextMeshProUGUI _search;
    private readonly TextMeshProUGUI _filters;
    private readonly RectTransform _listPane;
    private readonly TextMeshProUGUI _list;
    private readonly RectTransform _detailPane;
    private readonly RectTransform _detailContent;
    private readonly TextMeshProUGUI _detail;
    private readonly TextMeshProUGUI _footer;

    private readonly HashSet<string> _revealed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<Page> _history = new();

    private string _query = "";
    private int _categoryIndex = -1; // -1 = all categories
    private bool _seenOnly;
    private List<GlossaryEntry> _results = new();
    private int _selected = -1;
    private int _listOffset;
    private Page _page = Page.Welcome;
    private Task<LookupResult>? _pending;
    private float _detailScroll;
    private float _detailHeight;
    private bool _listDirty = true;
    private bool _detailDirty = true;
    private bool _caretOn;

    public DictionaryWindow(Transform canvas, TMP_FontAsset? font, ModContext ctx)
    {
        _ctx = ctx;

        _root = UiKit.Rect("DiscoDictionary.Window", canvas);
        UiKit.Place(_root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height));
        UiKit.Background(_root, UiKit.PanelColor);

        var titleRect = UiKit.Rect("Title", _root);
        UiKit.TopBand(titleRect, 16f, 36f, Pad, Pad);
        _title = UiKit.Text(titleRect, font, 26f, UiKit.AccentColor, wrap: false);

        var closeRect = UiKit.Rect("Close", _root);
        UiKit.TopBand(closeRect, 20f, 30f, Pad, Pad);
        _close = UiKit.Text(closeRect, font, 17f, UiKit.MutedColor, wrap: false);
        _close.alignment = TextAlignmentOptions.TopRight;

        var searchRect = UiKit.Rect("Search", _root);
        UiKit.TopBand(searchRect, 60f, 32f, Pad, Pad);
        _search = UiKit.Text(searchRect, font, 21f, UiKit.TextColor, wrap: false);

        var filterRect = UiKit.Rect("Filters", _root);
        UiKit.TopBand(filterRect, 98f, 56f, Pad, Pad);
        _filters = UiKit.Text(filterRect, font, 16f, UiKit.MutedColor);

        _listPane = UiKit.Rect("List", _root);
        _listPane.anchorMin = new Vector2(0f, 0f);
        _listPane.anchorMax = new Vector2(0f, 1f);
        _listPane.pivot = new Vector2(0f, 1f);
        _listPane.offsetMin = new Vector2(Pad, Pad + FooterHeight);
        _listPane.offsetMax = new Vector2(Pad + ListWidth, -BodyTop);
        UiKit.Background(_listPane, UiKit.PaneColor);
        UiKit.Clip(_listPane);
        var listTextRect = UiKit.Rect("ListText", _listPane);
        UiKit.Stretch(listTextRect, InnerPad, InnerPad, InnerPad, InnerPad);
        _list = UiKit.Text(listTextRect, font, ListFontSize, UiKit.TextColor, wrap: false);

        _detailPane = UiKit.Rect("Detail", _root);
        UiKit.Stretch(_detailPane, Pad + ListWidth + 16f, BodyTop, Pad, Pad + FooterHeight);
        UiKit.Background(_detailPane, UiKit.PaneColor);
        UiKit.Clip(_detailPane);
        _detailContent = UiKit.Rect("DetailText", _detailPane);
        _detailContent.anchorMin = new Vector2(0f, 1f);
        _detailContent.anchorMax = new Vector2(1f, 1f);
        _detailContent.pivot = new Vector2(0.5f, 1f);
        _detailContent.sizeDelta = new Vector2(-InnerPad * 2f - 8f, 100f);
        _detailContent.anchoredPosition = new Vector2(0f, -InnerPad);
        _detail = UiKit.Text(_detailContent, font, DetailFontSize, UiKit.TextColor);
        _detail.paragraphSpacing = 8f;

        var footerRect = UiKit.Rect("Footer", _root);
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.offsetMin = new Vector2(Pad, 10f);
        footerRect.offsetMax = new Vector2(-Pad, 10f + FooterHeight);
        _footer = UiKit.Text(footerRect, font, 15f, UiKit.MutedColor, wrap: false);
        _footer.overflowMode = TextOverflowModes.Ellipsis;

        _root.gameObject.SetActive(false);
    }

    private enum PageKind
    {
        Welcome,
        Entry,
        English,
    }

    private sealed class Page
    {
        public static readonly Page Welcome = new(PageKind.Welcome, null, null, null);

        public Page(PageKind kind, GlossaryEntry? entry, string? word, LookupResult? english)
        {
            Kind = kind;
            Entry = entry;
            Word = word;
            English = english;
        }

        public PageKind Kind { get; }
        public GlossaryEntry? Entry { get; }
        public string? Word { get; }
        public LookupResult? English { get; set; }
    }

    public bool IsOpen => _root.gameObject.activeSelf;

    public void Open()
    {
        if (IsOpen)
            return;
        _root.gameObject.SetActive(true);
        RefreshResults(keepSelection: true);
        _detailDirty = true;
    }

    public void Close()
    {
        _root.gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public bool ContainsMouse(Vector3 mouse) => IsOpen && UiKit.Contains(_root, mouse);

    /// <summary>Called after the glossary was reloaded: old entry objects are stale.</summary>
    public void OnContentReloaded()
    {
        _history.Clear();
        if (_page.Kind == PageKind.Entry && _page.Entry != null)
        {
            var fresh = _ctx.Content.Glossary.Get(_page.Entry.Id);
            _page = fresh != null ? new Page(PageKind.Entry, fresh, null, null) : Page.Welcome;
        }
        RefreshResults(keepSelection: true);
        _detailDirty = true;
    }

    public void ShowEntry(GlossaryEntry entry, bool remember = true)
    {
        if (remember && _page.Kind != PageKind.Welcome && _page.Entry != entry)
            _history.Push(_page);
        _ctx.Seen.MarkSeen(entry.Id);
        _page = new Page(PageKind.Entry, entry, null, null);
        _pending = null;
        int index = _results.IndexOf(entry);
        if (index >= 0)
        {
            _selected = index;
            EnsureSelectionVisible();
        }
        ScrollDetailTo(0f);
        _listDirty = true;
        _detailDirty = true;
    }

    public void ShowEnglish(string word, bool remember = true)
    {
        if (remember && _page.Kind != PageKind.Welcome)
            _history.Push(_page);
        _page = new Page(PageKind.English, null, word, null);
        _pending = null;
        if (!_ctx.Settings.OnlineEnglishLookup.Value)
        {
            _page.English = new LookupResult(word, LookupStatus.Disabled,
                message: "Online lookups are turned off. Set OnlineEnglishLookup = true in UserData/MelonPreferences.cfg to turn them on.");
        }
        else
        {
            _pending = _ctx.English.LookupAsync(word);
        }
        ScrollDetailTo(0f);
        _detailDirty = true;
    }

    /// <summary>Puts a word in the search box (used when the player looks up a word from the game).</summary>
    public void SetQuery(string query)
    {
        _query = query.Length > MaxQueryLength ? query.Substring(0, MaxQueryLength) : query;
        RefreshResults(keepSelection: false);
    }

    public void Update(Vector3 mouse, float now)
    {
        if (!IsOpen)
            return;

        HandleKeyboard();
        HandleMouse(mouse);
        PollLookup();

        bool caret = ((int)(now * 2f) & 1) == 0;
        if (caret != _caretOn)
        {
            _caretOn = caret;
            RenderSearch();
        }

        if (_listDirty)
            RenderList();
        if (_detailDirty)
            RenderDetail();

        // Keep the game from also reacting to the keys typed into the search box.
        GameInput.Suppress();
    }

    private void HandleKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        string typed = Input.inputString ?? "";
        bool changed = false;
        foreach (char c in typed)
        {
            if (c == '\b')
            {
                if (_query.Length > 0)
                {
                    _query = _query.Substring(0, _query.Length - 1);
                    changed = true;
                }
            }
            else if (c == '\n' || c == '\r')
            {
                Submit();
            }
            else if (!char.IsControl(c) && _query.Length < MaxQueryLength)
            {
                _query += c;
                changed = true;
            }
        }

        if (changed)
        {
            RefreshResults(keepSelection: false);
            // Live preview of the best match while typing.
            if (_query.Length > 0 && _results.Count > 0)
                ShowEntry(_results[0], remember: false);
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
            MoveSelection(+1);
        if (Input.GetKeyDown(KeyCode.UpArrow))
            MoveSelection(-1);
        if (Input.GetKeyDown(KeyCode.PageDown))
            ScrollDetailTo(_detailScroll + 400f);
        if (Input.GetKeyDown(KeyCode.PageUp))
            ScrollDetailTo(_detailScroll - 400f);
    }

    private void HandleMouse(Vector3 mouse)
    {
        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0f)
        {
            if (UiKit.Contains(_listPane, mouse))
            {
                _listOffset = Math.Max(0, Math.Min(Math.Max(0, _results.Count - VisibleRows()), _listOffset - (int)Math.Round(wheel * 3f)));
                _listDirty = true;
            }
            else if (UiKit.Contains(_detailPane, mouse))
            {
                ScrollDetailTo(_detailScroll - wheel * WheelStep);
            }
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        // Picking from the list replaces the page; following a link inside an explanation can go "Back".
        string? listLink = UiKit.Contains(_listPane, mouse) ? UiKit.LinkAt(_list, mouse) : null;
        if (listLink != null)
        {
            Follow(listLink, remember: false);
            return;
        }

        string? link = UiKit.LinkAt(_close, mouse)
                       ?? UiKit.LinkAt(_filters, mouse)
                       ?? UiKit.LinkAt(_footer, mouse)
                       ?? (UiKit.Contains(_detailPane, mouse) ? UiKit.LinkAt(_detail, mouse) : null);
        if (link != null)
            Follow(link);
    }

    /// <summary>Acts on a clicked link. Also used for sidebar clicks.</summary>
    public void Follow(string link, bool remember = true)
    {
        if (link == LinkClose)
        {
            Close();
        }
        else if (link == LinkBack)
        {
            if (_history.Count > 0)
            {
                _page = _history.Pop();
                if (_page.Kind == PageKind.English && _page.English == null && _page.Word != null)
                    _pending = _ctx.English.LookupAsync(_page.Word);
                ScrollDetailTo(0f);
                _detailDirty = true;
            }
        }
        else if (link == LinkLookup)
        {
            if (_query.Trim().Length > 0)
                ShowEnglish(_query.Trim());
        }
        else if (link == LinkReload)
        {
            _ctx.ReloadContent();
        }
        else if (link == LinkMore)
        {
            _listOffset = Math.Min(Math.Max(0, _results.Count - VisibleRows()), _listOffset + VisibleRows() - 1);
            _listDirty = true;
        }
        else if (link == LinkAll)
        {
            _categoryIndex = -1;
            _seenOnly = false;
            RefreshResults(keepSelection: true);
        }
        else if (link == LinkSeen)
        {
            _seenOnly = !_seenOnly;
            RefreshResults(keepSelection: true);
        }
        else if (link.StartsWith(LinkCategory, StringComparison.Ordinal))
        {
            if (int.TryParse(link.Substring(LinkCategory.Length), out int index))
                _categoryIndex = _categoryIndex == index ? -1 : index;
            RefreshResults(keepSelection: true);
        }
        else if (link.StartsWith(EntryFormatter.SpoilerLinkPrefix, StringComparison.Ordinal))
        {
            _revealed.Add(link.Substring(EntryFormatter.SpoilerLinkPrefix.Length));
            _detailDirty = true;
        }
        else if (link.StartsWith(EntryFormatter.EntryLinkPrefix, StringComparison.Ordinal))
        {
            var entry = _ctx.Content.Glossary.Get(link.Substring(EntryFormatter.EntryLinkPrefix.Length));
            if (entry != null)
            {
                Open();
                ShowEntry(entry, remember);
            }
        }
    }

    private void Submit()
    {
        string q = _query.Trim();
        if (q.Length == 0)
            return;

        var exact = _ctx.Content.Glossary.FindByName(q);
        if (exact != null && _ctx.IsVisible(exact))
            ShowEntry(exact);
        else if (_ctx.Settings.OnlineEnglishLookup.Value || _results.Count == 0)
            ShowEnglish(q);
        else
            ShowEntry(_results[0]);
    }

    private void MoveSelection(int delta)
    {
        if (_results.Count == 0)
            return;
        _selected = Math.Max(0, Math.Min(_results.Count - 1, _selected + delta));
        ShowEntry(_results[_selected], remember: false);
    }

    private void RefreshResults(bool keepSelection)
    {
        var current = _page.Kind == PageKind.Entry ? _page.Entry : null;
        string? category = _categoryIndex >= 0 && _categoryIndex < Categories.All.Count ? Categories.All[_categoryIndex] : null;
        IEnumerable<GlossaryEntry> results = _ctx.Content.Glossary.Search(_query, _ctx.IsVisible, category);
        if (_seenOnly)
            results = results.Where(e => _ctx.Seen.IsSeen(e.Id));
        _results = results.ToList();

        _selected = keepSelection && current != null ? _results.IndexOf(current) : (_results.Count > 0 && _query.Length > 0 ? 0 : -1);
        _listOffset = 0;
        EnsureSelectionVisible();
        _listDirty = true;
        RenderSearch();
        RenderFilters();
        RenderFooter();
    }

    private int VisibleRows()
    {
        float height = _listPane.rect.height - InnerPad * 2f;
        int rows = (int)(height / (ListFontSize * 1.36f));
        return Math.Max(5, rows) - (_query.Length > 0 ? 1 : 0);
    }

    private void EnsureSelectionVisible()
    {
        if (_selected < 0)
            return;
        int rows = VisibleRows();
        if (_selected < _listOffset)
            _listOffset = _selected;
        else if (_selected >= _listOffset + rows)
            _listOffset = _selected - rows + 1;
    }

    private void PollLookup()
    {
        if (_pending == null || !_pending.IsCompleted)
            return;

        LookupResult result;
        if (_pending.Status == TaskStatus.RanToCompletion)
            result = _pending.Result;
        else
            result = new LookupResult(_page.Word ?? "", LookupStatus.Error, message: _pending.Exception?.GetBaseException().Message ?? "Lookup failed.");
        _pending = null;

        // Make online-lookup problems visible in the MelonLoader console, where they can be diagnosed.
        if (result.Status == LookupStatus.Error)
            _ctx.Log.Warning($"English lookup of '{result.Query}' failed: {result.Message}");

        if (_page.Kind == PageKind.English)
        {
            _page.English = result;
            _detailDirty = true;
        }
    }

    private void ScrollDetailTo(float y)
    {
        float viewport = _detailPane.rect.height - InnerPad * 2f;
        float max = Math.Max(0f, _detailHeight - viewport);
        _detailScroll = Math.Max(0f, Math.Min(max, y));
        _detailContent.anchoredPosition = new Vector2(0f, _detailScroll - InnerPad);
    }

    // ---------------------------------------------------------------- rendering

    private void RenderSearch()
    {
        _title.text = "<b>DISCO DICTIONARY</b>";
        _close.text = RichText.Link(LinkClose, "[ Close: Esc or " + RichText.Escape(_ctx.Settings.OpenKeyCode.ToString()) + " ]");

        string caret = _caretOn ? "_" : " ";
        string shown = _query.Length == 0
            ? RichText.Color(EntryFormatter.MutedColor, "type to search...") + caret
            : RichText.Escape(_query) + caret;
        _search.text = RichText.Color(EntryFormatter.HeadingColor, "Search: ") + shown;
    }

    private void RenderFilters()
    {
        var sb = new StringBuilder();
        sb.Append(FilterLink(LinkAll, "All", _categoryIndex < 0 && !_seenOnly, "FFFFFF"));
        sb.Append("   ");
        sb.Append(FilterLink(LinkSeen, "Seen in game", _seenOnly, "FFFFFF"));
        for (int i = 0; i < Categories.All.Count; i++)
        {
            sb.Append("   ");
            string name = Categories.All[i];
            sb.Append(FilterLink(LinkCategory + i, name, _categoryIndex == i, Categories.Color(name)));
        }
        _filters.text = sb.ToString();
    }

    private static string FilterLink(string id, string label, bool active, string color)
    {
        string text = active ? "<b><u>" + RichText.Escape(label) + "</u></b>" : RichText.Escape(label);
        return RichText.Link(id, RichText.Color(active ? color : EntryFormatter.MutedColor, text));
    }

    private void RenderFooter()
    {
        var g = _ctx.Content.Glossary;
        string lookupKey = RichText.Escape(_ctx.Settings.LookupKeyCode.ToString());
        // The reload link goes first so it can never be cut off on narrow screens.
        _footer.text =
            RichText.Link(LinkReload, RichText.Color(EntryFormatter.LinkColor, "Reload glossary files"))
            + $"   |   {g.Count} entries, {_ctx.Seen.Count} seen in game   |   Up/Down: browse   |   Enter: look up what you typed   |   "
            + $"In game: {lookupKey}{(_ctx.Settings.MiddleClickLookup.Value ? " or middle-click" : "")} looks up the word under the mouse";
    }

    private void RenderList()
    {
        _listDirty = false;
        var sb = new StringBuilder();

        if (_query.Length > 0)
        {
            string label = _ctx.Settings.OnlineEnglishLookup.Value
                ? "Look up \"" + RichText.Escape(Truncate(_query, 18)) + "\" in English"
                : "English lookups are off";
            sb.Append(RichText.Link(LinkLookup, RichText.Color(EntryFormatter.LinkColor, "<i>" + label + "</i>"))).Append('\n');
        }

        if (_results.Count == 0)
        {
            sb.Append(RichText.Color(EntryFormatter.MutedColor, _query.Length > 0 ? "No dictionary entries match." : "Nothing here yet."));
            _list.text = sb.ToString();
            return;
        }

        int rows = VisibleRows();
        int end = Math.Min(_results.Count, _listOffset + rows);
        for (int i = _listOffset; i < end; i++)
        {
            var e = _results[i];
            string term = RichText.Escape(Truncate(e.Term, 30));
            string line = i == _selected
                ? RichText.Color("FFD27F", "<b>> " + term + "</b>")
                : "  " + term;
            sb.Append(RichText.Link(EntryFormatter.EntryLinkPrefix + e.Id, line));
            if (i < end - 1)
                sb.Append('\n');
        }

        if (end < _results.Count)
            sb.Append('\n').Append(RichText.Link(LinkMore, RichText.Color(EntryFormatter.MutedColor, $"  ... {_results.Count - end} more (scroll)")));

        _list.text = sb.ToString();
    }

    private void RenderDetail()
    {
        _detailDirty = false;
        var sb = new StringBuilder();
        if (_history.Count > 0)
            sb.Append(RichText.Link(LinkBack, RichText.Color(EntryFormatter.LinkColor, "< Back"))).Append("\n\n");

        switch (_page.Kind)
        {
            case PageKind.Entry when _page.Entry != null:
                bool revealed = _ctx.Settings.RevealSpoilers.Value || _revealed.Contains(_page.Entry.Id);
                sb.Append(_ctx.Content.Formatter.FormatEntry(_page.Entry, revealed));
                break;

            case PageKind.English:
                if (_page.English != null)
                    sb.Append(EntryFormatter.FormatEnglish(_page.English));
                else
                    sb.Append("<size=150%><b>").Append(RichText.Escape(_page.Word ?? "")).Append("</b></size>\n\n")
                      .Append(RichText.Color(EntryFormatter.MutedColor, "Looking it up..."));
                break;

            default:
                sb.Append(WelcomeText());
                break;
        }

        string content = sb.ToString();
        _detail.text = content;
        _detailHeight = UiKit.PreferredHeight(_detail, content) + 16f;
        _detailContent.sizeDelta = new Vector2(_detailContent.sizeDelta.x, _detailHeight);
        ScrollDetailTo(_detailScroll);
    }

    private string WelcomeText()
    {
        var s = _ctx.Settings;
        string lookupKey = RichText.Escape(s.LookupKeyCode.ToString());
        return "<size=150%><b>Welcome to the Disco Dictionary</b></size>\n\n"
               + "Disco Elysium throws a whole invented world at you: places, politics, slang, and 24 voices in your head. This dictionary explains it as you go, without spoiling the story.\n\n"
               + RichText.Color(EntryFormatter.HeadingColor, "<b>While you play</b>") + "\n"
               + "The sidebar (" + RichText.Escape(s.SidebarKeyCode.ToString()) + " to hide or show it) lists terms from the latest lines of dialogue with a one-line explanation. Click one to read more.\n\n"
               + "Point at <i>any</i> word on screen and press " + lookupKey + (s.MiddleClickLookup.Value ? " (or click the middle mouse button)" : "")
               + ". Game terms open their entry; ordinary English words are looked up in an online dictionary.\n\n"
               + RichText.Color(EntryFormatter.HeadingColor, "<b>In this window</b>") + "\n"
               + "Type to search. Click a category above to browse it. Blue words inside an explanation are links. Spoilers stay hidden until you click them, and some names only appear once you've met them in the game.\n\n"
               + RichText.Color(EntryFormatter.HeadingColor, "<b>Add your own</b>") + "\n"
               + "Put your own entries in " + RichText.Escape(_ctx.Content.UserGlossaryDir) + " (there's an example file), then click \"Reload glossary files\" below.";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 3) + "...";
}
