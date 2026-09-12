using System.Text.Json;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Handles generic settings features such as loading, saving, and replacing the current value.
/// </summary>
/// <typeparam name="T">The type of data in which to store settings.</typeparam>
public abstract class SettingsProviderBase<T> : ISettingsProvider<T>
    where T : class, new()
{
    private readonly ISerializationService _serialization;

    /// <summary>
    /// Creates a provider that serializes settings through <paramref name="serializationService"/>.
    /// </summary>
    protected SettingsProviderBase(ISerializationService serializationService)
    {
        _serialization = serializationService ?? throw new ArgumentNullException(nameof(serializationService));
    }

    /// <inheritdoc />
    public T Value
    {
        get;
        set
        {
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    } = new();

    /// <summary>
    /// Gets the path where settings are stored.
    /// </summary>
    public abstract string FilePath { get; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public event EventHandler? Saving;

    /// <inheritdoc />
    public abstract T Load();

    /// <inheritdoc />
    public T Load(string path)
    {
        T? result = null;
        try
        {
            result = _serialization.DeserializeFromFile<T>(path);
        }
        catch (JsonException) { }
        catch (InvalidOperationException) { }
        catch (DirectoryNotFoundException) { }
        catch (FileNotFoundException) { }

        Value = result ?? GetDefault();
        Changed?.Invoke(this, EventArgs.Empty);
        return Value;
    }

    /// <inheritdoc />
    public abstract void Save();

    /// <inheritdoc />
    public void Save(string path)
    {
        Saving?.Invoke(this, EventArgs.Empty);
        _serialization.SerializeToFile(Value, path);
    }

    /// <summary>
    /// When overridden in a derived class, returns the default settings values.
    /// </summary>
    protected virtual T GetDefault() => new();
}
