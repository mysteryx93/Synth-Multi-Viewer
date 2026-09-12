namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Manages JSON serialization of objects to and from disk.
/// </summary>
public interface ISerializationService
{
    /// <summary>
    /// Saves an object to a JSON file.
    /// </summary>
    void SerializeToFile<T>(T dataToSerialize, string path);

    /// <summary>
    /// Loads an object of specified type from a JSON file.
    /// </summary>
    T DeserializeFromFile<T>(string path) where T : class, new();
}
