using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
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
    public void ClickEmptyContentArea_FocusesEditorWithoutOpeningDropdown()
    {
        var view = new MainView { DataContext = TestSupport.CreateMain() };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var box = Editor(combo);

        // The editable area extends to the drop-down button, including beyond the written text.
        Assert.InRange(box.Bounds.Width, combo.Bounds.Width - 26, combo.Bounds.Width - 22);
        Assert.InRange(box.Bounds.Height, combo.Bounds.Height - 1, combo.Bounds.Height + 1);
        Click(view, combo.TranslatePoint(new Point(combo.Bounds.Width - 36, 3), view)!.Value);

        Assert.True(box.IsFocused);
        Assert.False(combo.IsDropDownOpen);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveEditor_InBothThemes_KeepsVisibleOuterBorder(bool dark)
    {
        var view = new MainView
        {
            DataContext = TestSupport.CreateMain(),
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var border = combo.GetTemplateDescendants().OfType<Border>().Single(x => x.Name == "Background");
        Click(view, combo.TranslatePoint(new Point(20, 14), view)!.Value);
        view.MouseMove(new Point(5, 100));
        Dispatcher.UIThread.RunJobs();

        Assert.True(Editor(combo).IsFocused);
        AssertVisibleBorder(border);
        Click(view, combo.TranslatePoint(new Point(combo.Bounds.Width - 12, 14), view)!.Value);
        Assert.True(combo.IsDropDownOpen);
        AssertVisibleBorder(border);
        TestSupport.Press(view, Key.Escape);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZoomCombo_UsesCompactFieldAndButtonOnlyInteractionSurface(bool dark)
    {
        var view = new MainView
        {
            DataContext = TestSupport.CreateMain(),
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var editor = Editor(combo);
        var highlight = combo.GetTemplateDescendants().OfType<Border>()
            .Single(x => x.Name == "HighlightBackground");
        var dropDownButton = combo.GetTemplateDescendants().OfType<Border>()
            .Single(x => x.Name == "DropDownOverlay");
        var glyph = combo.GetTemplateDescendants().OfType<PathIcon>()
            .Single(x => x.Name == "DropDownGlyph");

        editor.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(combo.Width, combo.Bounds.Width);
        Assert.Equal(13, combo.FontSize);
        Assert.Equal(13, editor.FontSize);
        Assert.Equal(10, glyph.Bounds.Width);
        Assert.Equal(10, glyph.Bounds.Height);
        Assert.True(dropDownButton.IsVisible);
        Assert.Equal(24, dropDownButton.Bounds.Width);
        var buttonLeft = dropDownButton.TranslatePoint(default, combo)!.Value.X;
        var glyphLeft = glyph.TranslatePoint(default, combo)!.Value.X;
        Assert.InRange(glyphLeft + glyph.Bounds.Width / 2,
            buttonLeft + dropDownButton.Bounds.Width / 2 - 1,
            buttonLeft + dropDownButton.Bounds.Width / 2 + 1);
        var textPresenter = editor.GetTemplateDescendants().OfType<TextPresenter>().Single();
        Assert.True(textPresenter.Bounds.Width >= combo.Bounds.Width - 32);
        var highlightBrush = Assert.IsAssignableFrom<ISolidColorBrush>(highlight.Background);
        Assert.True(highlightBrush.Color.A == 0 || highlightBrush.Opacity == 0);

        Click(view, combo.TranslatePoint(new Point(combo.Bounds.Width - 12, 14), view)!.Value);
        Assert.True(combo.IsDropDownOpen);
        var editorRight = editor.TranslatePoint(new Point(editor.Bounds.Width, 0), combo)!.Value.X;
        Assert.InRange(buttonLeft - editorRight, -1, 2);
        TestSupport.Press(view, Key.Escape);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DropDownButton_PressAndOpen_KeepPressedSurfaceVisible(bool dark)
    {
        var view = new MainView
        {
            DataContext = TestSupport.CreateMain(),
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var dropDownButton = combo.GetTemplateDescendants().OfType<Border>()
            .Single(x => x.Name == "DropDownOverlay");
        var point = combo.TranslatePoint(new Point(combo.Bounds.Width - 12, 14), view)!.Value;

        view.MouseMove(point);
        view.MouseDown(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var box = Editor(combo);
        Assert.Equal(box.SelectionStart, box.SelectionEnd);
        AssertVisibleFill(dropDownButton);

        view.MouseUp(point, MouseButton.Left);
        view.MouseMove(new Point(5, 100));
        Dispatcher.UIThread.RunJobs();

        Assert.True(combo.IsDropDownOpen);
        AssertVisibleFill(dropDownButton);

        TestSupport.Press(view, Key.Escape);
        Dispatcher.UIThread.RunJobs();

        Assert.False(combo.IsDropDownOpen);
        AssertTransparentFill(dropDownButton);
    }

    [AvaloniaFact]
    public void OpenDropDown_ByButton_DoesNotSelectEditorText()
    {
        var view = new MainView { DataContext = TestSupport.CreateMain() };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var box = Editor(combo);

        Click(view, combo.TranslatePoint(new Point(combo.Bounds.Width - 12, 14), view)!.Value);

        Assert.True(combo.IsDropDownOpen);
        Assert.Equal("100%", box.Text);
        Assert.Equal(box.SelectionStart, box.SelectionEnd);
        TestSupport.Press(view, Key.Escape);
    }

    [AvaloniaFact]
    public void SelectPreset_FromDropDown_DoesNotSelectEditorText()
    {
        var view = new MainView { DataContext = TestSupport.CreateMain() };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var box = Editor(combo);
        Click(view, combo.TranslatePoint(new Point(combo.Bounds.Width - 12, 14), view)!.Value);
        var item = combo.ContainerFromIndex(5)!;
        var popupRoot = TopLevel.GetTopLevel(item)!;

        Click(popupRoot, item.TranslatePoint(new Point(20, item.Bounds.Height / 2), popupRoot)!.Value);

        Assert.False(combo.IsDropDownOpen);
        Assert.Equal("150%", box.Text);
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

        Assert.True(combo.IsDropDownOpen);
        Assert.Equal("100%", box.Text);
        Assert.Equal(box.SelectionStart, box.SelectionEnd);
        TestSupport.Press(view, Key.Escape);
    }

    [AvaloniaFact]
    public void DropDownButton_KeyboardOpen_UsesSameActiveSurface()
    {
        var view = new MainView { DataContext = TestSupport.CreateMain() };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var dropDownButton = combo.GetTemplateDescendants().OfType<Border>()
            .Single(x => x.Name == "DropDownOverlay");
        Editor(combo).Focus();

        TestSupport.Press(view, Key.F4);
        Dispatcher.UIThread.RunJobs();

        Assert.True(combo.IsDropDownOpen);
        AssertVisibleFill(dropDownButton);
        TestSupport.Press(view, Key.Escape);
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertVisibleBorder(Border border)
    {
        Assert.True(border.IsEffectivelyVisible);
        Assert.True(border.BorderThickness.Left > 0 && border.BorderThickness.Top > 0 &&
                    border.BorderThickness.Right > 0 && border.BorderThickness.Bottom > 0);
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(border.BorderBrush);
        Assert.True(brush.Color.A > 0 && brush.Opacity > 0);
    }

    private static void AssertVisibleFill(Border border)
    {
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(border.Background);
        Assert.True(brush.Color.A > 0 && brush.Opacity > 0);
    }

    private static void AssertTransparentFill(Border border)
    {
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(border.Background);
        Assert.True(brush.Color.A == 0 || brush.Opacity == 0);
    }

    [AvaloniaTheory]
    [InlineData("150%", 1.5, "150%")]
    [InlineData("abc", 1, "100%")]
    public void ClickBlankSpace_CommitsOrRestoresTextAndReleasesFocus(string text, double zoom, string display)
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var box = Editor(combo);
        box.Focus();
        box.SelectAll();
        view.KeyTextInput(text);

        Click(view, new Point(20, view.Bounds.Height - 20));

        Assert.False(combo.IsKeyboardFocusWithin);
        Assert.Equal(zoom, model.Zoom, precision: 8);
        Assert.Equal(display, box.Text);
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter, 1.37, "137%")]
    [InlineData(Key.Escape, 1, "100%")]
    public void FinishEditing_WithKeyboard_CommitsOrCancelsAndReleasesFocus(Key key, double zoom, string display)
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
        Assert.Equal(display, box.Text);
        Assert.False(combo.IsKeyboardFocusWithin);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SelectPresets_WithDefaultOrOverlayPopup_UpdatesZoomAndRetainsClickAway(bool overlay, bool reenable)
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        var popup = combo.GetTemplateDescendants().OfType<Popup>().Single();
        popup.ShouldUseOverlayLayer = overlay;
        if (reenable)
        {
            ComboBoxCommit.SetEnabled(combo, false);
            ComboBoxCommit.SetEnabled(combo, true);
        }

        (int Index, double Zoom, string? PendingText)[] selections =
            [(0, 0, null), (5, 1.5, "137%"), (2, 0.5, "abc"), (7, 4, null)];
        foreach (var (index, zoom, pendingText) in selections)
        {
            Editor(combo).Focus();
            if (pendingText != null)
            {
                Editor(combo).SelectAll();
                view.KeyTextInput(pendingText);
            }
            Click(view, combo.TranslatePoint(new Point(combo.Bounds.Width - 12, 14), view)!.Value);
            Assert.True(combo.IsDropDownOpen);
            if (overlay) { Assert.True(popup.IsUsingOverlayLayer); }

            var item = combo.ContainerFromIndex(index)!;
            var popupRoot = TopLevel.GetTopLevel(item)!;
            Click(popupRoot, item.TranslatePoint(new Point(20, item.Bounds.Height / 2), popupRoot)!.Value);

            Assert.False(combo.IsDropDownOpen);
            Assert.Equal(model.ZoomList[index], combo.SelectedItem);
            Assert.Equal(zoom, model.Zoom, precision: 8);
            Assert.Equal(zoom == 0, model.ZoomScaleToFit);
            Assert.Equal(model.ZoomList[index], Editor(combo).Text);

            Click(view, new Point(20, view.Bounds.Height - 20));

            Assert.False(combo.IsKeyboardFocusWithin);
            Assert.Equal(zoom, model.Zoom, precision: 8);
            Assert.Equal(model.ZoomList[index], combo.Text);
        }
    }

    [AvaloniaFact]
    public void ClickBlankSpace_WithDropdownOpen_DismissesAndReleasesFocus()
    {
        var view = new MainView { DataContext = TestSupport.CreateMain() };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        Editor(combo).Focus();
        Click(view, combo.TranslatePoint(new Point(combo.Bounds.Width - 12, 14), view)!.Value);
        Assert.True(combo.IsDropDownOpen);

        Click(view, new Point(20, view.Bounds.Height - 20));

        Assert.False(combo.IsDropDownOpen);
        Assert.False(combo.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public void DisableBehavior_DetachesClickAwayAndCommitHandlers()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        var combo = ViewerZoom(view);
        ComboBoxCommit.SetEnabled(combo, false);
        Editor(combo).Focus();
        Editor(combo).SelectAll();
        view.KeyTextInput("137%");

        Click(view, new Point(20, view.Bounds.Height - 20));

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
