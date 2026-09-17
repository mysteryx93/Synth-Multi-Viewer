using HanumanInstitute.ScriptAssist.VapourSynth;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests;

public class ScriptLanguageFactoryTests
{
    [Fact]
    public void HostConstructor_RegistersVapourSynthAndAviSynth()
    {
        var factory = new ScriptLanguageFactory(() => [], () => []);

        Assert.True(factory.IsEnabled);
        Assert.NotNull(factory.Create(ScriptLanguageFactory.VapourSynth));
        Assert.NotNull(factory.Create(ScriptLanguageFactory.AviSynth));
        Assert.Null(factory.Create("missing"));
    }

    [Fact]
    public async Task DisabledFactorySkipsEnumerationUntilEnabled()
    {
        var count = 0;
        var catalog = new CatalogCache(() =>
        {
            Interlocked.Increment(ref count);
            return (IReadOnlyList<Symbol>)[];
        });
        var factory = new ScriptLanguageFactory(
            [new LanguageProfile("one", new VapourSynthLanguage(), catalog)]);

        factory.IsEnabled = false;
        factory.Configure("one", "a");
        Assert.Null(factory.Create("one"));
        Assert.Equal(0, count);
        factory.IsEnabled = true;
        factory.Configure("one", "a");
        await catalog.GetAsync(CancellationToken.None);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RetainedServiceSkipsEnumerationWhileDisabled()
    {
        var count = 0;
        var catalog = new CatalogCache(() =>
        {
            Interlocked.Increment(ref count);
            return (IReadOnlyList<Symbol>)[];
        });
        var factory = new ScriptLanguageFactory(
            [new LanguageProfile("one", new VapourSynthLanguage(), catalog)]);
        var service = factory.Create("one")!;
        factory.IsEnabled = false;
        var reply = await service.GetAsync("clip", 4, CancellationToken.None);
        Assert.Empty(reply.Items);
        Assert.Equal(0, count);
    }

    [Fact]
    public void Create_UnknownLanguage_ReturnsNull()
    {
        var factory = new ScriptLanguageFactory(
            [new LanguageProfile("one", new VapourSynthLanguage(), new CatalogCache(() => []))]);

        Assert.Null(factory.Create("missing"));
        Assert.NotNull(factory.Create("one"));
    }

    [Fact]
    public void Configure_UnknownLanguage_DoesNotThrow()
    {
        var factory = new ScriptLanguageFactory([]);
        factory.Configure("missing", "key");
    }

    [Fact]
    public void DuplicateLanguageId_Throws()
    {
        var language = new VapourSynthLanguage();
        var catalog = new CatalogCache(() => []);

        Assert.Throws<ArgumentException>(() => new ScriptLanguageFactory(
        [
            new LanguageProfile("one", language, catalog),
            new LanguageProfile("one", language, catalog)
        ]));
    }
}
