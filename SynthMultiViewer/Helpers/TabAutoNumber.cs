using System.Globalization;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Picks the lowest unused "Prefix N" title among open tabs.
/// </summary>
public static class TabAutoNumber
{
    /// <summary>
    /// Returns the smallest positive integer not already used as <c>prefix N</c>.
    /// </summary>
    public static int Next(IEnumerable<string> titles, string prefix)
    {
        var head = prefix + " ";
        var used = new HashSet<int>();
        foreach (var name in titles)
        {
            if (name.StartsWith(head, StringComparison.Ordinal) &&
                int.TryParse(name.AsSpan(head.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) &&
                n > 0)
            {
                used.Add(n);
            }
        }

        var next = 1;
        while (used.Contains(next))
        {
            next++;
        }

        return next;
    }
}
