using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <inheritdoc />
public class ProcessService : IProcessService
{
    /// <inheritdoc />
    public void OpenBrowserUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true, Verb = "open" });
        }
        catch
        {
            // https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }
}
