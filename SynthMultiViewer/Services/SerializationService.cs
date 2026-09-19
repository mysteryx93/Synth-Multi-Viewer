using System.Text.Json;
using HanumanInstitute.ScriptAssist.Services;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <inheritdoc />
public class SerializationService : ISerializationService
{
    private readonly IFileSystemService _files;

    /// <summary>
    /// Creates a serializer that reads and writes through <paramref name="files"/>.
    /// </summary>
    public SerializationService(IFileSystemService files)
    {
        _files = files.CheckNotNull();
    }

    /// <inheritdoc />
    public void SerializeToFile<T>(T dataToSerialize, string path)
    {
        _files.EnsureDirectoryExists(path);
        using var writer = _files.FileStream.New(path, System.IO.FileMode.Create);
        JsonSerializer.Serialize(writer, dataToSerialize, typeof(T), AppJsonContext.Default);
        writer.Flush();
    }

    /// <inheritdoc />
    public T DeserializeFromFile<T>(string path) where T : class, new()
    {
        using var stream = _files.File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, typeof(T), AppJsonContext.Default) as T ?? new T();
    }
}
