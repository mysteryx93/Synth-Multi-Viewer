using System.IO.Abstractions;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Parses extra folder lists entered in Settings.
/// </summary>
public static class FolderListText
{
    /// <summary>
    /// Splits a folder list on newlines or the OS path separator.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? text, IPath paths)
    {
        paths.CheckNotNull();
        if (!text.HasText())
        {
            return [];
        }

        return text
            .Split(['\r', '\n', paths.PathSeparator, ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Returns the last parsed folder, or null when the list is empty.
    /// </summary>
    public static string? Last(string? text, IPath paths)
    {
        var folders = Parse(text, paths);
        return folders.Count > 0 ? folders[^1] : null;
    }

    /// <summary>
    /// Adds a folder to the list when it is not already present.
    /// </summary>
    public static string Append(string? text, string folder, IPath paths)
    {
        if (!folder.HasText())
        {
            return text ?? "";
        }

        var folders = Parse(text, paths).ToList();
        var trimmed = folder.Trim();
        if (!folders.Contains(trimmed, StringComparer.Ordinal))
        {
            folders.Add(trimmed);
        }

        return string.Join("; ", folders);
    }
}
