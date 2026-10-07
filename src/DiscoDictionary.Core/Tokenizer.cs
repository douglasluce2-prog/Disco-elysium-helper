using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DiscoDictionary.Core;

/// <summary>A word in a piece of text, with its position so matches can be mapped back to characters.</summary>
public readonly struct Token
{
    public Token(int start, int end, string word, bool joinsPrevious)
    {
        Start = start;
        End = end;
        Word = word;
        JoinsPrevious = joinsPrevious;
    }

    /// <summary>Index of the first character in the source text.</summary>
    public int Start { get; }

    /// <summary>Index one past the last character in the source text (includes any possessive "'s").</summary>
    public int End { get; }

    /// <summary>The word with a possessive "'s" removed and curly apostrophes straightened, original case.</summary>
    public string Word { get; }

    /// <summary>Lower-case form of <see cref="Word"/>, used as the lookup key.</summary>
    public string Key => Word.ToLowerInvariant();

    /// <summary>
    /// True when only spaces or hyphens separate this token from the previous one, so the two can be
    /// part of the same multi-word term ("Inland Empire", "Whirling-in-Rags"). Punctuation such as
    /// ". , ; : ( )" or a dash breaks the phrase.
    /// </summary>
    public bool JoinsPrevious { get; }

    public override string ToString() => Word;
}

public static class Tokenizer
{
    private const int MaxJoinGap = 3;

    public static List<Token> Tokenize(string? text)
    {
        var tokens = new List<Token>();
        if (string.IsNullOrEmpty(text))
            return tokens;

        int i = 0;
        int previousEnd = -1;
        var word = new StringBuilder();

        while (i < text.Length)
        {
            if (!IsWordChar(text[i]))
            {
                i++;
                continue;
            }

            int start = i;
            word.Clear();
            while (i < text.Length)
            {
                char c = text[i];
                if (IsWordChar(c))
                {
                    word.Append(c);
                    i++;
                }
                else if (IsApostrophe(c) && i + 1 < text.Length && IsWordChar(text[i + 1]) && word.Length > 0)
                {
                    // Apostrophe inside a word: "don't", "Revachol's", "Ma'am".
                    word.Append('\'');
                    i++;
                }
                else
                {
                    break;
                }
            }

            int end = i;
            string w = word.ToString();
            if (w.Length > 2 && (w.EndsWith("'s") || w.EndsWith("'S")))
                w = w.Substring(0, w.Length - 2);

            // Trailing apostrophe of a plural possessive ("the Hardie Boys' bar") is not part of the word.
            if (i < text.Length && IsApostrophe(text[i]))
                i++;

            bool joins = previousEnd >= 0 && IsJoinGap(text, previousEnd, start);
            tokens.Add(new Token(start, end, w, joins));
            previousEnd = i;
        }

        return tokens;
    }

    /// <summary>The word (possessive removed) covering <paramref name="index"/>, or null if it isn't inside a word.</summary>
    public static string? WordAt(string? text, int index)
    {
        if (string.IsNullOrEmpty(text) || index < 0 || index >= text!.Length)
            return null;
        foreach (var token in Tokenize(text))
        {
            if (index >= token.Start && index < token.End)
                return token.Word;
        }
        return null;
    }

    public static bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;

    public static bool IsApostrophe(char c) => c == '\'' || c == '\u2019' || c == '\u02BC';

    private static bool IsJoinGap(string text, int from, int to)
    {
        int length = to - from;
        if (length < 1 || length > MaxJoinGap)
            return false;

        for (int k = from; k < to; k++)
        {
            char c = text[k];
            // Spaces, hyphens, and the slash in "Hand/Eye Coordination".
            bool ok = c == ' ' || c == '\u00A0' || c == '\t' || c == '-' || c == '\u2010' || c == '\u2011' || c == '/';
            if (!ok)
                return false;
        }
        return true;
    }
}
