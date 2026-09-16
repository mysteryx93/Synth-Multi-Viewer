using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class AppThemeLayoutTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DialogButtons_InBothThemes_CenterTextWithoutClipping(bool dark)
    {
        Window[] dialogs =
        [
            new InputView { DataContext = new InputViewModel { Text = "Go to frame", Value = "100" } },
            new HelpView { DataContext = new HelpViewModel(new TestSupport.TestEnvironment()) },
            new SettingsView { DataContext = TestSupport.CreateViewModel(typeof(SettingsViewModel)) },
            new TabColorView { DataContext = new TabColorViewModel() }
        ];
        foreach (var dialog in dialogs)
        {
            dialog.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            using var shown = TestSupport.Show(dialog);
            var buttons = dialog.GetVisualDescendants().OfType<Button>()
                .Where(button => button.GetType() == typeof(Button) && button.Content is string).ToList();
            Assert.NotEmpty(buttons);
            foreach (var button in buttons)
            {
                var label = button.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Text == (string)button.Content!);
                var origin = label.TranslatePoint(default, button)!.Value;
                Assert.InRange(origin.X + label.Bounds.Width / 2, button.Bounds.Width / 2 - 1, button.Bounds.Width / 2 + 1);
                Assert.InRange(origin.Y + label.Bounds.Height / 2, button.Bounds.Height / 2 - 1, button.Bounds.Height / 2 + 1);
                Assert.True(label.Bounds.Height <= button.Bounds.Height - button.Padding.Top - button.Padding.Bottom);
            }
        }
    }

    [AvaloniaFact]
    public void Window_Shown_UsesLayoutRounding()
    {
        Window[] windows =
        [
            new MainView { DataContext = TestSupport.CreateMain() },
            new InputView { DataContext = new InputViewModel { Text = "Go to frame", Value = "100" } },
            new HelpView { DataContext = new HelpViewModel(new TestSupport.TestEnvironment()) },
            new SettingsView { DataContext = TestSupport.CreateViewModel(typeof(SettingsViewModel)) },
            new TabColorView { DataContext = new TabColorViewModel() }
        ];

        foreach (var window in windows)
        {
            using var shown = TestSupport.Show(window);

            Assert.True(window.UseLayoutRounding);
        }
    }

    [AvaloniaFact]
    public void DarkTheme_SurfacePalette_IsLiftedFromBlack()
    {
        var window = new MainView { DataContext = TestSupport.CreateMain() };
        window.RequestedThemeVariant = ThemeVariant.Dark;
        using var shown = TestSupport.Show(window);

        Assert.True(window.TryGetResource("SystemRegionColor", ThemeVariant.Dark, out var region));
        Assert.True(window.TryGetResource("SystemChromeLowColor", ThemeVariant.Dark, out var chrome));
        Assert.Equal(Color.Parse("#FF1C1C1C"), region);
        Assert.Equal(Color.Parse("#FF242424"), chrome);
        Assert.NotEqual(Colors.Black, region);
    }

    [AvaloniaFact]
    public void TabColorView_Shown_ContainsColorView()
    {
        var view = new TabColorView { DataContext = new TabColorViewModel() };

        using var shown = TestSupport.Show(view);

        Assert.NotNull(view.GetVisualDescendants().OfType<ColorView>().FirstOrDefault());
        Assert.False(view.CanResize);
        Assert.False(view.ShowInTaskbar);
    }
}
