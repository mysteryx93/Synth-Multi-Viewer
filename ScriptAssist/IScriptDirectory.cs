namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Plugin or script search roots and the files in them.
/// </summary>
public interface IScriptDirectory
{
    /// <summary>
    /// Gets directories searched for autoload scripts.
    /// </summary>
    IReadOnlyList<string> Roots();

    /// <summary>
    /// Lists files in <paramref name="directory"/> whose extension is in <paramref name="extensions"/>.
    /// </summary>
    IEnumerable<string> Files(string directory, IReadOnlyList<string> extensions);

    /// <summary>
    /// Returns the file contents, or null when the path cannot be read.
    /// </summary>
    string? TryRead(string path);
}
