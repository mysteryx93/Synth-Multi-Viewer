using System.IO.Abstractions;

namespace HanumanInstitute.ScriptAssist.Services;

/// <summary>
/// Extends <see cref="IFileSystem"/> with a few extra IO functions.
/// Lives in this folder so it can move to a shared services library later.
/// </summary>
public interface IFileSystemService : IFileSystem
{
    /// <summary>
    /// Ensures the directory of specified path exists. If it doesn't exist, creates the directory.
    /// </summary>
    void EnsureDirectoryExists(string path);

    /// <summary>
    /// Deletes a file if it exists.
    /// </summary>
    void DeleteFileSilent(string path);

    /// <summary>
    /// Returns all files of specified extensions.
    /// </summary>
    IEnumerable<string> GetFilesByExtensions(string path, IEnumerable<string> extensions,
        System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Returns specified path without its file extension.
    /// </summary>
    string GetPathWithoutExtension(string path);

    /// <summary>
    /// Returns the path ensuring it ends with a directory separator char.
    /// </summary>
    string GetPathWithFinalSeparator(string path);

    /// <summary>
    /// Replaces all illegal chars in specified file name with specified replacement character.
    /// </summary>
    string SanitizeFileName(string fileName, char replacementChar = '_');
}
