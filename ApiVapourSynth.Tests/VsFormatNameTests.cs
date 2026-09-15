using Xunit;

namespace HanumanInstitute.ApiVapourSynth.Tests;

public class VsFormatNameTests
{
    [Fact]
    public void From_Yuv420P10_ReturnsName()
    {
        var format = new VsFormat
        {
            ColorFamily = VsColorFamily.YUV,
            SampleType = VsSampleType.Integer,
            BitsPerSample = 10,
            SubSamplingW = 1,
            SubSamplingH = 1,
            NumPlanes = 3
        };

        Assert.Equal("YUV420P10", VsFormatName.From(format));
        Assert.Equal("YUV420P10", format.Name);
        Assert.Equal("4:2:0", VsFormatName.Subsampling(format));
    }

    [Theory]
    [InlineData(VsColorFamily.RGB, VsSampleType.Integer, 8, 0, 0, "RGB24", null)]
    [InlineData(VsColorFamily.RGB, VsSampleType.Float, 32, 0, 0, "RGBS", null)]
    [InlineData(VsColorFamily.Gray, VsSampleType.Integer, 8, 0, 0, "GRAY8", null)]
    [InlineData(VsColorFamily.Gray, VsSampleType.Integer, 32, 0, 0, "GRAY32", null)]
    [InlineData(VsColorFamily.Gray, VsSampleType.Float, 32, 0, 0, "GRAYS", null)]
    [InlineData(VsColorFamily.YUV, VsSampleType.Integer, 8, 1, 1, "YUV420P8", "4:2:0")]
    [InlineData(VsColorFamily.YUV, VsSampleType.Float, 32, 0, 0, "YUV444PS", "4:4:4")]
    public void From_PresetLayout_ReturnsName(
        VsColorFamily family, VsSampleType sample, int bits, int subW, int subH, string name, string? sub)
    {
        var format = new VsFormat
        {
            ColorFamily = family,
            SampleType = sample,
            BitsPerSample = bits,
            SubSamplingW = subW,
            SubSamplingH = subH,
            NumPlanes = family == VsColorFamily.Gray ? 1 : 3
        };

        Assert.Equal(name, VsFormatName.From(format));
        Assert.Equal(sub, VsFormatName.Subsampling(format));
    }
}
