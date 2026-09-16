using System.Globalization;

namespace HanumanInstitute.SynthMultiViewer.Services.Completion;

/// <summary>
/// Position-preserving lexer; offsets remain AvaloniaEdit UTF-16 offsets.
/// </summary>
public static class BufferLexer
{
    /// <summary>
    /// Recognizes UTF-16 identifier characters, including combining marks and surrogate pairs.
    /// </summary>
    public static bool IsIdentifier(char c) => char.IsLetterOrDigit(c) || c == '_' || char.IsSurrogate(c) ||
        char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

    /// <summary>
    /// Blanks comments and optionally strings while preserving original document offsets.
    /// </summary>
    public static LexedBuffer Mask(string text, bool aviSynth, bool maskStrings = true)
    {
        var code = text.ToCharArray();
        var quote = '\0';
        var triple = false;
        var line = false;
        var block = new Stack<char>();
        void Hide(int index)
        {
            if (code[index] != '\n' && code[index] != '\r')
            {
                code[index] = ' ';
            }
        }
        void HideString(int index)
        {
            if (maskStrings)
            {
                Hide(index);
            }
        }
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (line)
            {
                if (c == '\n')
                {
                    line = false;
                }
                else
                {
                    Hide(i);
                }
                continue;
            }
            if (block.Count > 0)
            {
                Hide(i);
                if (c == block.Peek() && next == '/')
                {
                    Hide(++i);
                    block.Pop();
                }
                else if (c == '/' && next == '*')
                {
                    Hide(++i);
                    block.Push('*');
                }
                continue;
            }
            if (quote != '\0')
            {
                HideString(i);
                if (!aviSynth && c == '\\' && next != '\0')
                {
                    HideString(++i);
                    continue;
                }
                if (c == quote)
                {
                    if (triple)
                    {
                        if (next == quote && i + 2 < text.Length && text[i + 2] == quote)
                        {
                            HideString(++i);
                            HideString(++i);
                            quote = '\0';
                        }
                    }
                    else if (aviSynth && next == quote)
                    {
                        HideString(++i);
                    }
                    else
                    {
                        quote = '\0';
                    }
                }
                continue;
            }
            if (c == '#')
            {
                line = true;
                Hide(i);
            }
            else if (aviSynth && c == '/' && next is '*' or '[')
            {
                block.Push(next == '*' ? '*' : ']');
                Hide(i);
                Hide(++i);
            }
            else if (c == '"' || (!aviSynth && c == '\''))
            {
                quote = c;
                triple = next == c && i + 2 < text.Length && text[i + 2] == c;
                HideString(i);
                if (triple)
                {
                    HideString(++i);
                    HideString(++i);
                }
            }
        }
        return new(new string(code), quote != '\0' || line || block.Count > 0);
    }
}

/// <summary>
/// Position-preserving code and whether the buffer ends inside a comment or string.
/// </summary>
public sealed record LexedBuffer(string Code, bool InLiteral);
