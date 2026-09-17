using System.Text.Json;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <inheritdoc />
public class SerializationService : ISerializationService
{
    /// <inheritdoc />
    public void SerializeToFile<T>(T dataToSerialize, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory.HasValue())
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = File.Create(path);
        JsonSerializer.Serialize(writer, dataToSerialize, typeof(T), AppJsonContext.Default);
        writer.Flush();
    }

    /// <inheritdoc />
    public T DeserializeFromFile<T>(string path) where T : class, new()
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, typeof(T), AppJsonContext.Default) as T ?? new T();
    }
}
