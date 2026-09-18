namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// One task per configuration, including failures. Cancellation never restarts native enumeration.
/// </summary>
public sealed class CatalogCache(Func<IReadOnlyList<Symbol>> enumerate) : ISymbolCatalog
{
    private readonly Lock _gate = new();
    private Task<IReadOnlyList<Symbol>>? _task;
    private string? _key;

    /// <summary>
    /// Gets the configuration key last passed to <see cref="SetKey"/> or <see cref="Refresh"/>.
    /// </summary>
    internal string? Key
    {
        get
        {
            lock (_gate)
            {
                return _key;
            }
        }
    }

    /// <inheritdoc />
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
                    return enumerate().ToArray();
                }
                catch (Exception)
                {
                    return (IReadOnlyList<Symbol>)[];
                }
            });
        }
    }

    /// <inheritdoc />
    public void SetKey(string key)
    {
        lock (_gate)
        {
            if (_key == key)
            {
                return;
            }

            _key = key;
            _task = null;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Symbol>> GetAsync(CancellationToken token)
    {
        lock (_gate)
        {
            if (_task == null)
            {
                Refresh(_key ?? "");
            }

            return _task!.WaitAsync(token);
        }
    }
}
