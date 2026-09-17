using Xunit;

namespace HanumanInstitute.ApiAviSynth.Tests;

public class AvsImportGraphTests
{
    [Fact]
    public void ThrowIfRecursive_SelfImport_Throws()
    {
        using var files = new TempScripts();
        var path = files.Write("loop.avs", "Import(\"loop.avs\")\nBlankClip()\n");

        var error = Assert.Throws<AvsException>(() =>
            AvsImportGraph.ThrowIfRecursive(File.ReadAllText(path), path));

        Assert.Contains("cycle", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThrowIfRecursive_MutualImport_Throws()
    {
        using var files = new TempScripts();
        var a = files.Write("a.avs", "Import(\"b.avs\")\n");
        files.Write("b.avs", "Import(\"a.avs\")\nBlankClip()\n");

        Assert.Throws<AvsException>(() => AvsImportGraph.ThrowIfRecursive(File.ReadAllText(a), a));
    }

    [Fact]
    public void ThrowIfRecursive_AcyclicImport_DoesNotThrow()
    {
        using var files = new TempScripts();
        var a = files.Write("a.avs", "Import(\"b.avs\")\n");
        files.Write("b.avs", "BlankClip()\n");

        AvsImportGraph.ThrowIfRecursive(File.ReadAllText(a), a);
    }

    private sealed class TempScripts : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TempScripts() => Directory.CreateDirectory(_dir);

        public string Write(string name, string text)
        {
            var path = Path.Combine(_dir, name);
            File.WriteAllText(path, text);
            return path;
        }

        public void Dispose() => Directory.Delete(_dir, true);
    }
}
