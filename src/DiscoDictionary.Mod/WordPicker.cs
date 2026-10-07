using System;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;

namespace DiscoDictionary;

/// <summary>What the player pointed at.</summary>
internal sealed class PickedWord
{
    public PickedWord(string plainText, int charIndex, string word)
    {
        PlainText = plainText;
        CharIndex = charIndex;
        Word = word;
    }

    /// <summary>The whole text (markup removed) the word belongs to, so multi-word terms can be found.</summary>
    public string PlainText { get; }

    /// <summary>Index of the word's first character in <see cref="PlainText"/>.</summary>
    public int CharIndex { get; }

    public string Word { get; }
}

/// <summary>
/// Finds the word under the mouse cursor in any TextMeshPro text on screen: dialogue, tooltips,
/// the journal, Thought Cabinet descriptions, item descriptions, and the dictionary itself.
/// </summary>
internal static class WordPicker
{
    /// <param name="mouse">Mouse position in screen pixels.</param>
    /// <param name="ownRoot">The dictionary's own canvas.</param>
    /// <param name="fromOwnUi">True to pick only from the dictionary's texts (mouse is over our window),
    /// false to pick only from the game's.</param>
    public static PickedWord? Pick(Vector3 mouse, Transform ownRoot, bool fromOwnUi)
    {
        var all = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<TMP_Text>());
        if (all == null)
            return null;

        // Several texts can overlap (a tooltip over the dialogue log); prefer the one drawn on top.
        PickedWord? best = null;
        int bestOrder = int.MinValue;
        foreach (var obj in all)
        {
            var text = obj?.TryCast<TMP_Text>();
            if (text == null || !text.isActiveAndEnabled)
                continue;
            if (text.transform.IsChildOf(ownRoot) != fromOwnUi)
                continue;

            try
            {
                var canvas = text.canvas;
                int order = canvas == null ? int.MinValue + 1 : canvas.sortingOrder;
                if (best != null && order <= bestOrder)
                    continue;

                var picked = PickFrom(text, mouse);
                if (picked != null)
                {
                    best = picked;
                    bestOrder = order;
                }
            }
            catch (Exception)
            {
                // A text that is being rebuilt this frame can throw; just skip it.
            }
        }
        return best;
    }

    private static PickedWord? PickFrom(TMP_Text text, Vector3 mouse)
    {
        Camera? camera = null;
        var canvas = text.canvas;
        if (canvas == null)
            camera = Camera.main;
        else if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            camera = canvas.worldCamera;

        if (!RectTransformUtility.RectangleContainsScreenPoint(text.rectTransform, new Vector2(mouse.x, mouse.y), camera))
            return null;

        int wordIndex = TMP_TextUtilities.FindIntersectingWord(text, mouse, camera);
        if (wordIndex < 0)
            return null;

        var info = text.textInfo;
        if (info == null || wordIndex >= info.wordCount)
            return null;

        var wordInfo = info.wordInfo[wordIndex];
        string word = wordInfo.GetWord();
        int charIndex = wordInfo.firstCharacterIndex;
        string plain = text.GetParsedText() ?? "";
        if (string.IsNullOrWhiteSpace(word) || charIndex < 0 || charIndex >= plain.Length)
            return null;

        return new PickedWord(plain, charIndex, word);
    }
}
