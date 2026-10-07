using System.Text;
using System.Text.RegularExpressions;

namespace DiscoDictionary.Core;

/// <summary>Helpers for TextMeshPro rich text (the markup the game and our UI use).</summary>
public static class RichText
{
    private static readonly Regex Tag = new("<[^<>]{1,200}>", RegexOptions.Compiled);

    /// <summary>Removes markup such as &lt;i&gt; or &lt;color=#fff&gt; so only the readable text is left.</summary>
    public static string Strip(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        return Tag.Replace(text!, "");
    }

    /// <summary>Makes arbitrary text safe to embed in TextMeshPro markup.</summary>
    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        if (text!.IndexOf('<') < 0)
            return text;
        return "<noparse>" + text.Replace("</noparse>", "</ noparse>") + "</noparse>";
    }

    public static string Color(string hex, string text) => $"<color=#{hex}>{text}</color>";

    public static string Link(string id, string text) => $"<link=\"{id}\">{text}</link>";

    /// <summary>Collapses runs of whitespace (the game's lines sometimes contain newlines and double spaces).</summary>
    public static string CollapseWhitespace(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        var sb = new StringBuilder(text!.Length);
        bool space = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!space && sb.Length > 0)
                    sb.Append(' ');
                space = true;
            }
            else
            {
                sb.Append(c);
                space = false;
            }
        }
        return sb.ToString().TrimEnd();
    }
}
