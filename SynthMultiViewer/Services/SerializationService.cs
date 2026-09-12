using System.Text.Json;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <inheritdoc />
public class SerializationService : ISerializationService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public void SerializeToFile<T>(T dataToSerialize, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = File.Create(path);
        JsonSerializer.Serialize(writer, dataToSerialize, typeof(T), Options);
        writer.Flush();
    }

    /// <inheritdoc />
    public T DeserializeFromFile<T>(string path) where T : class, new()
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, Options) ?? new T();
    }
}
