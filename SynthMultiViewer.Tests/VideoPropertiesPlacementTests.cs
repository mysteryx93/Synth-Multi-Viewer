using Avalonia;
using HanumanInstitute.SynthMultiViewer.Helpers;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class VideoPropertiesPlacementTests
{
    [Fact]
    public void AlignToOwnerRight_MatchesOwnerRightEdgeAndVerticalCenter()
    {
        var position = VideoPropertiesPlacement.AlignToOwnerRight(
            new PixelPoint(100, 40), new PixelSize(800, 600), new PixelSize(380, 460));

        Assert.Equal(new PixelPoint(520, 110), position);
    }

    [Fact]
    public void AlignToOwnerRight_AccountsForChildChrome()
    {
        var position = VideoPropertiesPlacement.AlignToOwnerRight(
            new PixelPoint(100, 40), new PixelSize(800, 600), new PixelSize(400, 480));

        Assert.Equal(new PixelPoint(500, 100), position);
    }
}
