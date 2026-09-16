using Avalonia;
using Avalonia.Controls;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Session size and screen position of the video properties window.
/// Bound two-way by the properties window for the current player instance.
/// </summary>
public partial class VideoPropertiesPlacement : ReactiveObject
{
    /// <summary>
    /// Gets or sets the restored window width.
    /// </summary>
    [Reactive]
    public partial double Width { get; set; } = 380;

    /// <summary>
    /// Gets or sets the restored window height.
    /// </summary>
    [Reactive]
    public partial double Height { get; set; } = 460;

    /// <summary>
    /// Gets or sets the restored screen X position. Null uses the first-show default.
    /// </summary>
    [Reactive]
    public partial int? Left { get; set; }

    /// <summary>
    /// Gets or sets the restored screen Y position. Null uses the first-show default.
    /// </summary>
    [Reactive]
    public partial int? Top { get; set; }

    /// <summary>
    /// Places a window on the owner's right edge, vertically centered so it does not cover the toolbar.
    /// Sizes are outer frames in pixels so window chrome does not hang past the owner.
    /// </summary>
    public static PixelPoint AlignToOwnerRight(
        PixelPoint ownerPosition, PixelSize ownerFrameSize, PixelSize childFrameSize)
    {
        return new PixelPoint(
            ownerPosition.X + ownerFrameSize.Width - childFrameSize.Width,
            ownerPosition.Y + (ownerFrameSize.Height - childFrameSize.Height) / 2);
    }

    /// <summary>
    /// Returns the owner's outer size in pixels, falling back to DIP size when FrameSize is unset.
    /// </summary>
    public static PixelSize OwnerFrameSize(Window owner)
    {
        var scale = Math.Max(owner.DesktopScaling, 0.01);
        if (owner.FrameSize is { Width: > 0, Height: > 0 } frame)
        {
            return PixelSize.FromSize(frame, scale);
        }

        return PixelSize.FromSize(new Size(owner.Width, owner.Height), scale);
    }

    /// <summary>
    /// Estimates the properties window's outer size in pixels from its client size plus the owner's chrome.
    /// </summary>
    public static PixelSize ChildFrameSize(Window owner, double width, double height)
    {
        var scale = Math.Max(owner.DesktopScaling, 0.01);
        var size = new Size(width, height);
        if (owner is { FrameSize: { Width: > 0, Height: > 0 } frame, ClientSize: { Width: > 0, Height: > 0 } client })
        {
            size += new Size(
                Math.Max(0, frame.Width - client.Width),
                Math.Max(0, frame.Height - client.Height));
        }

        return PixelSize.FromSize(size, scale);
    }
}
