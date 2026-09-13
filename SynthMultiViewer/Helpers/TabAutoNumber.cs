using System.Globalization;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Picks the next "Prefix N" title as one past the highest open tab of that prefix.
/// </summary>
public static class TabAutoNumber
{
    /// <summary>
    /// Returns one greater than the largest <c>prefix N</c> among <paramref name="titles"/>, or 1 when none exist.
    /// </summary>
    public static int Next(IEnumerable<string> titles, string prefix)
    {
        var head = prefix + " ";
        var max = 0;
        foreach (var name in titles)
        {
            if (name.StartsWith(head, StringComparison.Ordinal) &&
                int.TryParse(name.AsSpan(head.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) &&
                n > max)
            {
                max = n;
            }
        }

        return max + 1;
    }
}
