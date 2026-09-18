using HanumanInstitute.SynthMultiViewer.Services;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class FolderListTextTests
{
    [Fact]
    public void Parse_Empty_ReturnsEmpty()
    {
        var folders = FolderListText.Parse("  ");

        Assert.Empty(folders);
    }

    [Fact]
    public void Parse_NewlinesAndBlanks_ReturnsDistinctFolders()
    {
        const string text = "/opt/a\n\n/opt/b\n/opt/a";

        var folders = FolderListText.Parse(text);

        Assert.Equal(["/opt/a", "/opt/b"], folders);
    }

    [Fact]
    public void Parse_PathSeparator_SplitsFolders()
    {
        var text = "/opt/a" + Path.PathSeparator + " /opt/b ";

        var folders = FolderListText.Parse(text);

        Assert.Equal(["/opt/a", "/opt/b"], folders);
    }

    [Fact]
    public void Parse_Semicolon_SplitsFolders()
    {
        var folders = FolderListText.Parse("/opt/a; /opt/b ;/opt/a");

        Assert.Equal(["/opt/a", "/opt/b"], folders);
    }

    [Fact]
    public void Last_Empty_ReturnsNull()
    {
        Assert.Null(FolderListText.Last("  "));
    }

    [Fact]
    public void Last_SeveralFolders_ReturnsLast()
    {
        Assert.Equal("/opt/b", FolderListText.Last("/opt/a; /opt/b"));
    }

    [Fact]
    public void Append_NewFolder_AddsToList()
    {
        var folders = FolderListText.Append("/opt/a", "/opt/b");

        Assert.Equal("/opt/a; /opt/b", folders);
    }

    [Fact]
    public void Append_ExistingFolder_DoesNotDuplicate()
    {
        var folders = FolderListText.Append("/opt/a; /opt/b", "/opt/a");

        Assert.Equal("/opt/a; /opt/b", folders);
    }

    [Fact]
    public void Append_EmptyList_ReturnsFolder()
    {
        var folders = FolderListText.Append("", "/opt/a");

        Assert.Equal("/opt/a", folders);
    }
}
