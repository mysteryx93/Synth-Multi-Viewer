namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Parses extra folder lists entered in Settings.
/// </summary>
public static class FolderListText
{
    /// <summary>
    /// Splits a folder list on newlines or the OS path separator.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(['\r', '\n', Path.PathSeparator, ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Returns the last parsed folder, or null when the list is empty.
    /// </summary>
    public static string? Last(string? text)
    {
        var folders = Parse(text);
        return folders.Count > 0 ? folders[^1] : null;
    }

    /// <summary>
    /// Adds a folder to the list when it is not already present.
    /// </summary>
    public static string Append(string? text, string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return text ?? "";
        }

        var folders = Parse(text).ToList();
        var trimmed = folder.Trim();
        if (!folders.Contains(trimmed, StringComparer.Ordinal))
        {
            folders.Add(trimmed);
        }

        return string.Join("; ", folders);
    }
}
