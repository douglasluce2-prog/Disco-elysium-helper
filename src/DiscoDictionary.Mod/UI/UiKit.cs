using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiscoDictionary.UI;

/// <summary>
/// Small helpers for building Unity UI from code. The dictionary uses the same UI system and
/// fonts as the game itself (uGUI + TextMeshPro), so it looks at home and needs no extra assets.
/// </summary>
internal static class UiKit
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    public static readonly Color PanelColor = new(0.055f, 0.052f, 0.06f, 0.93f);
    public static readonly Color SidebarColor = new(0.055f, 0.052f, 0.06f, 0.84f);
    public static readonly Color PaneColor = new(1f, 1f, 1f, 0.035f);
    public static readonly Color TextColor = new(0.86f, 0.84f, 0.80f, 1f);
    public static readonly Color MutedColor = new(0.55f, 0.54f, 0.52f, 1f);
    public static readonly Color AccentColor = new(0.79f, 0.65f, 0.42f, 1f);

    public static GameObject CreateCanvas(string name, float scale)
    {
        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth / scale, ReferenceHeight / scale);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // Lets our panels swallow clicks so clicking the dictionary doesn't also walk Harry somewhere.
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Fixed-size rectangle positioned relative to an anchor point of its parent.</summary>
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>Fill the parent, inset by the given margins.</summary>
    public static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Anchor to the top edge of the parent, full width, fixed height.</summary>
    public static void TopBand(RectTransform rt, float top, float height, float left, float right)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }

    public static Image Background(RectTransform rt, Color color)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    public static TextMeshProUGUI Text(RectTransform rt, TMP_FontAsset? font, float size, Color color, bool wrap = true)
    {
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.color = color;
        text.richText = true;
        text.enableWordWrapping = wrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    public static void Clip(RectTransform rt) => rt.gameObject.AddComponent<RectMask2D>();

    public static bool Contains(RectTransform? rt, Vector3 mouse) =>
        rt != null
        && rt.gameObject.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint(rt, new Vector2(mouse.x, mouse.y), null);

    /// <summary>The id of the &lt;link&gt; under the mouse in a text, or null.</summary>
    public static string? LinkAt(TMP_Text text, Vector3 mouse)
    {
        if (text == null || !text.gameObject.activeInHierarchy)
            return null;
        int index = TMP_TextUtilities.FindIntersectingLink(text, mouse, null);
        if (index < 0)
            return null;
        var info = text.textInfo;
        if (info == null || index >= info.linkCount)
            return null;
        return info.linkInfo[index].GetLinkID();
    }

    /// <summary>Height the text needs at the rectangle's current width.</summary>
    public static float PreferredHeight(TMP_Text text, string content)
    {
        float width = text.rectTransform.rect.width;
        return text.GetPreferredValues(content, width, 0f).y;
    }
}
