using System.Windows.Input;
using Avalonia.Headless.XUnit;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ScriptViewModelTests
{
    [AvaloniaFact]
    public void HeaderEditDone_PaddedName_TrimsName()
    {
        var model = new EditorViewModel { IsActive = true };
        ((ICommand)model.BeginHeaderEdit).Execute(null);
        model.DisplayName = "  Clip  ";

        ((ICommand)model.HeaderEditDone).Execute(null);

        Assert.Equal("Clip", model.DisplayName);
        Assert.False(model.IsEditingHeader);
    }

    [AvaloniaTheory]
    [InlineData("")]
    [InlineData("   ")]
    public void HeaderEditDone_BlankName_RestoresPreviousName(string name)
    {
        var model = new EditorViewModel { IsActive = true };
        ((ICommand)model.BeginHeaderEdit).Execute(null);
        model.DisplayName = name;

        ((ICommand)model.HeaderEditDone).Execute(null);

        Assert.Equal("Script", model.DisplayName);
        Assert.False(model.IsEditingHeader);
    }

    [AvaloniaFact]
    public void HeaderEditCancel_EditedName_RestoresPreviousName()
    {
        var model = new EditorViewModel { IsActive = true };
        ((ICommand)model.BeginHeaderEdit).Execute(null);
        model.DisplayName = "Renamed";

        ((ICommand)model.HeaderEditCancel).Execute(null);

        Assert.Equal("Script", model.DisplayName);
        Assert.False(model.IsEditingHeader);
    }
}
