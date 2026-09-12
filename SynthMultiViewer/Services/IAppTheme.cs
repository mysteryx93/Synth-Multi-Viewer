namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Applies the Fluent theme variant so it can be mocked in tests.
/// </summary>
public interface IAppTheme
{
    /// <summary>
    /// Gets or sets the desired theme mode (Light or Dark) for the app.
    /// </summary>
    string RequestedTheme { get; set; }
}
