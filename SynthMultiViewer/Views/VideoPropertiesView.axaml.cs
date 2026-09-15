using Avalonia.Controls;
using Avalonia.Threading;
using HanumanInstitute.SynthMultiViewer.Helpers;

namespace HanumanInstitute.SynthMultiViewer.Views;

/// <summary>
/// Displays clip and frame properties for the active viewer.
/// </summary>
public partial class VideoPropertiesView : Window
{
    /// <summary>
    /// Creates the view and loads its XAML.
    /// </summary>
    public VideoPropertiesView() => InitializeComponent();

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        WindowBounds.ApplyPosition(this);
        Dispatcher.UIThread.Post(() => WindowBounds.ApplyPosition(this), DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(() => WindowBounds.WatchPosition(this), DispatcherPriority.Background);
    }
}
