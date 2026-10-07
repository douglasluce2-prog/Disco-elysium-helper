using System.Text;
using DiscoDictionary.Core;
using Il2CppTMPro;
using UnityEngine;

namespace DiscoDictionary.UI;

/// <summary>
/// The small panel that lists terms from the most recent dialogue lines with a one-line
/// explanation each. Click a term to open its full entry.
/// </summary>
internal sealed class SidebarView
{
    private const float Width = 440f;
    private const float HeaderHeight = 30f;
    private const float Padding = 14f;
    private const float MaxHeight = 760f;

    private readonly RectTransform _root;
    private readonly TextMeshProUGUI _header;
    private readonly RectTransform _bodyRect;
    private readonly TextMeshProUGUI _body;
    private int _renderedVersion = -1;
    private long _renderedLine = -1;

    public SidebarView(Transform canvas, TMP_FontAsset? font, bool rightSide, string hint)
    {
        _root = UiKit.Rect("DiscoDictionary.Sidebar", canvas);
        var anchor = rightSide ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        UiKit.Place(_root, anchor, new Vector2(rightSide ? -24f : 24f, -110f), new Vector2(Width, 200f));
        UiKit.Background(_root, UiKit.SidebarColor);

        var headerRect = UiKit.Rect("Header", _root);
        UiKit.TopBand(headerRect, Padding - 4f, HeaderHeight, Padding, Padding);
        _header = UiKit.Text(headerRect, font, 15f, UiKit.AccentColor, wrap: false);
        _header.overflowMode = TextOverflowModes.Ellipsis;
        _header.text = "<b>DICTIONARY</b>   " + RichText.Color(EntryFormatter.MutedColor, "<size=85%>" + RichText.Escape(hint) + "</size>");

        _bodyRect = UiKit.Rect("Body", _root);
        UiKit.Stretch(_bodyRect, Padding, Padding + HeaderHeight, Padding, Padding);
        UiKit.Clip(_bodyRect);
        _body = UiKit.Text(_bodyRect, font, 19f, UiKit.TextColor);
        _body.lineSpacing = -4f;
        _body.paragraphSpacing = 6f;
    }

    public bool Visible => _root.gameObject.activeSelf;

    public void SetVisible(bool visible)
    {
        if (_root.gameObject.activeSelf != visible)
            _root.gameObject.SetActive(visible);
    }

    public void Render(LineAnalyzer analyzer)
    {
        if (analyzer.Version == _renderedVersion && analyzer.LineCount == _renderedLine)
            return;
        _renderedVersion = analyzer.Version;
        _renderedLine = analyzer.LineCount;

        var sb = new StringBuilder();
        foreach (var item in analyzer.Recent)
        {
            bool fresh = item.LineNumber == analyzer.LineCount;
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(RichText.Link(EntryFormatter.EntryLinkPrefix + item.Entry.Id,
                EntryFormatter.FormatSidebarItem(item.Entry, fresh, item.IsSpeaker)));
        }

        string content = sb.ToString();
        _body.text = content;

        float bodyHeight = UiKit.PreferredHeight(_body, content);
        float height = Mathf.Min(MaxHeight, bodyHeight + HeaderHeight + Padding * 2f + 4f);
        _root.sizeDelta = new Vector2(Width, height);
    }

    public bool ContainsMouse(Vector3 mouse) => Visible && UiKit.Contains(_root, mouse);

    public string? LinkAt(Vector3 mouse) => Visible ? UiKit.LinkAt(_body, mouse) : null;
}
