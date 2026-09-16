namespace HanumanInstitute.SynthMultiViewer.Services.Completion;

/// <summary>
/// One task per configuration, including failures. Cancellation never restarts native enumeration.
/// </summary>
public sealed class CatalogCache(Func<IReadOnlyList<FilterSymbol>> enumerate)
{
    private readonly Lock _gate = new();
    private Task<IReadOnlyList<FilterSymbol>>? _task;
    private string? _key;

    /// <summary>
    /// Starts background enumeration only when the configuration changes or a refresh is forced.
    /// </summary>
    public void Refresh(string key, bool force = false)
    {
        lock (_gate)
        {
            if (!force && _key == key && _task != null)
            {
                return;
            }
            _key = key;
            _task = Task.Run(() =>
            {
                try
                {
                    return enumerate();
                }
                catch (Exception)
                {
                    return [];
                }
            });
        }
    }

    /// <summary>
    /// Waits for the shared catalog without cancelling its native enumeration.
    /// </summary>
    public Task<IReadOnlyList<FilterSymbol>> GetAsync(CancellationToken token)
    {
        lock (_gate)
        {
            // Startup also works for editors hosted outside the application shell.
            if (_task == null)
            {
                Refresh(_key ?? "");
            }
            return _task!.WaitAsync(token);
        }
    }
}

