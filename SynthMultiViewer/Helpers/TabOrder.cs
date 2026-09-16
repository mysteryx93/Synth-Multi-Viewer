namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Moves a tab within the strip without wrapping.
/// </summary>
public static class TabOrder
{
    /// <summary>
    /// Shifts <paramref name="item"/> by <paramref name="delta"/> places when the destination is in range.
    /// </summary>
    public static bool TryMove<T>(ObservableCollection<T> items, T? item, int delta)
    {
        if (item is null || delta == 0)
        {
            return false;
        }

        var pos = items.IndexOf(item);
        var dest = pos + delta;
        if (pos < 0 || dest < 0 || dest >= items.Count)
        {
            return false;
        }

        items.Move(pos, dest);
        return true;
    }
}
