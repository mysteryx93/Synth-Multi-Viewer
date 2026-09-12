using Xunit;

namespace HanumanInstitute.ApiAviSynth.Tests;

public class AvsExceptionTests
{
    [Fact]
    public void Constructor_Message_PreservesText()
    {
        var message = "AviSynth could not import the script.";

        var error = new AvsException(message);

        Assert.Equal(message, error.Message);
    }
}
