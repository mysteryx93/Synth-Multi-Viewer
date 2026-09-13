using System.Collections.ObjectModel;
using HanumanInstitute.SynthMultiViewer.Helpers;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class TabOrderTests
{
    [Fact]
    public void TryMove_Right_SwapsWithNeighbor()
    {
        var items = new ObservableCollection<string> { "a", "b", "c" };

        Assert.True(TabOrder.TryMove(items, "a", 1));
        Assert.Equal(["b", "a", "c"], items);
    }

    [Fact]
    public void TryMove_Left_SwapsWithNeighbor()
    {
        var items = new ObservableCollection<string> { "a", "b", "c" };

        Assert.True(TabOrder.TryMove(items, "c", -1));
        Assert.Equal(["a", "c", "b"], items);
    }

    [Fact]
    public void TryMove_PastEnd_LeavesOrderUnchanged()
    {
        var items = new ObservableCollection<string> { "a", "b" };

        Assert.False(TabOrder.TryMove(items, "a", -1));
        Assert.False(TabOrder.TryMove(items, "b", 1));
        Assert.Equal(["a", "b"], items);
    }

    [Fact]
    public void TryMove_MissingItem_ReturnsFalse()
    {
        var items = new ObservableCollection<string> { "a" };

        Assert.False(TabOrder.TryMove(items, "missing", 1));
        Assert.False(TabOrder.TryMove<string>(items, null, 1));
    }
}
