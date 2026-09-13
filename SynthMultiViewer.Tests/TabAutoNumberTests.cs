using HanumanInstitute.SynthMultiViewer.Helpers;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class TabAutoNumberTests
{
    [Fact]
    public void Next_Empty_ReturnsOne()
    {
        Assert.Equal(1, TabAutoNumber.Next([], "Script"));
    }

    [Fact]
    public void Next_AfterClose_ReusesLowest()
    {
        Assert.Equal(1, TabAutoNumber.Next(["Script 2"], "Script"));
    }

    [Fact]
    public void Next_Gap_FillsLowestFree()
    {
        Assert.Equal(2, TabAutoNumber.Next(["Script 1", "Script 3"], "Script"));
    }

    [Fact]
    public void Next_IgnoresOtherPrefixesAndNonExactTitles()
    {
        Assert.Equal(1, TabAutoNumber.Next(["Viewer 1", "script.vpy", "Script 1 copy"], "Script"));
    }

    [Fact]
    public void Next_OpenFileDoesNotConsumeScriptNumbers()
    {
        Assert.Equal(2, TabAutoNumber.Next(["Script 1", "clip.vpy"], "Script"));
    }
}
