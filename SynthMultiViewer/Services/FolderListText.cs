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
}
