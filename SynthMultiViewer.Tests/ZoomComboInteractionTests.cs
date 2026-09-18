using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HanumanInstitute.SynthMultiViewer.Helpers;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ZoomComboInteractionTests
{
    [AvaloniaFact]
    public void Click_EmptyContentArea_FocusesEditorWithoutOpeningDropdown()
    {
        var view = new MainView { DataContext = TestSupport.CreateMain() };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var box = Editor(combo);

        Click(view, combo.TranslatePoint(new(20, combo.Bounds.Height / 2), view)!.Value);

        Assert.True(box.IsFocused);
        Assert.False(combo.IsDropDownOpen);
    }

    [AvaloniaFact]
    public void OpenDropDown_ByButton_DoesNotSelectEditorText()
    {
        var view = new MainView { DataContext = TestSupport.CreateMain() };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var box = Editor(combo);

        Click(view, combo.TranslatePoint(new(combo.Bounds.Width - 12, 14), view)!.Value);
        var dropDownOpen = combo.IsDropDownOpen;
        var start = box.SelectionStart;
        var end = box.SelectionEnd;
        TestSupport.Press(view, Key.Escape);

        Assert.True(dropDownOpen);
        Assert.Equal(start, end);
    }

    [AvaloniaFact]
    public void SelectPreset_FromDropDown_DoesNotSelectEditorText()
    {
        var view = new MainView { DataContext = TestSupport.CreateMain() };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var box = Editor(combo);
        Click(view, combo.TranslatePoint(new(combo.Bounds.Width - 12, 14), view)!.Value);
        var item = combo.ContainerFromIndex(5)!;
        var popupRoot = TopLevel.GetTopLevel(item)!;

        Click(popupRoot, item.TranslatePoint(new(20, item.Bounds.Height / 2), popupRoot)!.Value);

        Assert.False(combo.IsDropDownOpen);
        Assert.Equal(box.SelectionStart, box.SelectionEnd);
    }

    [AvaloniaFact]
    public void OpenDropDown_ByKeyboard_DoesNotSelectEditorText()
    {
        var view = new MainView { DataContext = TestSupport.CreateMain() };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var box = Editor(combo);
        box.Focus();
        box.SelectAll();

        TestSupport.Press(view, Key.F4);
        Dispatcher.UIThread.RunJobs();
        var dropDownOpen = combo.IsDropDownOpen;
        var start = box.SelectionStart;
        var end = box.SelectionEnd;
        TestSupport.Press(view, Key.Escape);

        Assert.True(dropDownOpen);
        Assert.Equal(start, end);
    }

    [AvaloniaTheory]
    [InlineData("150%", 1.5)]
    [InlineData("abc", 1)]
    public void Click_BlankSpace_CommitsOrRestoresAndReleasesFocus(string text, double zoom)
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var box = Editor(combo);
        box.Focus();
        box.SelectAll();
        view.KeyTextInput(text);

        Click(view, new(20, view.Bounds.Height - 20));

        Assert.False(combo.IsKeyboardFocusWithin);
        Assert.Equal(zoom, model.Zoom, precision: 8);
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter, 1.37)]
    [InlineData(Key.Escape, 1)]
    public void FinishEditing_WithKeyboard_CommitsOrCancelsAndReleasesFocus(Key key, double zoom)
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var box = Editor(combo);
        box.Focus();
        box.SelectAll();
        view.KeyTextInput("137%");

        TestSupport.Press(view, key);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(zoom, model.Zoom, precision: 8);
        Assert.False(combo.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public void SelectPreset_FromDropDown_UpdatesZoom()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        Editor(combo).Focus();
        Editor(combo).SelectAll();
        view.KeyTextInput("137%");
        Click(view, combo.TranslatePoint(new(combo.Bounds.Width - 12, 14), view)!.Value);
        var item = combo.ContainerFromIndex(5)!;
        var popupRoot = TopLevel.GetTopLevel(item)!;

        Click(popupRoot, item.TranslatePoint(new(20, item.Bounds.Height / 2), popupRoot)!.Value);

        Assert.False(combo.IsDropDownOpen);
        Assert.Equal(1.5, model.Zoom, precision: 8);
    }

    [AvaloniaFact]
    public void Click_BlankSpaceWithDropdownOpen_DismissesAndReleasesFocus()
    {
        var view = new MainView { DataContext = TestSupport.CreateMain() };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        Editor(combo).Focus();
        Click(view, combo.TranslatePoint(new(combo.Bounds.Width - 12, 14), view)!.Value);
        var opened = combo.IsDropDownOpen;

        Click(view, new(20, view.Bounds.Height - 20));

        Assert.True(opened);
        Assert.False(combo.IsDropDownOpen);
        Assert.False(combo.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public void ComboBoxCommit_Disabled_DetachesClickAwayAndCommit()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        ComboBoxCommit.SetEnabled(combo, false);
        Editor(combo).Focus();
        Editor(combo).SelectAll();
        view.KeyTextInput("137%");

        Click(view, new(20, view.Bounds.Height - 20));

        Assert.True(combo.IsKeyboardFocusWithin);
        Assert.Equal(1, model.Zoom);
    }

    private static ComboBox ViewerZoom(MainView view)
    {
        TestSupport.ShowViewerToolbar((MainViewModel)view.DataContext!);
        return view.FindControl<ComboBox>("ZoomCombo")!;
    }

    private static TextBox Editor(ComboBox combo) => combo.GetVisualDescendants().OfType<TextBox>()
        .Single(x => x.Name == "PART_EditableTextBox");

    private static void Click(TopLevel window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
