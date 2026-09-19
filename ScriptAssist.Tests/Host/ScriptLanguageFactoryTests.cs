using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Xunit;
// ReSharper disable AccessToModifiedClosure

namespace HanumanInstitute.ScriptAssist.Tests;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class ScriptLanguageFactoryTests
{
    [Fact]
    public void HostConstructor_RegistersVapourSynthAndAviSynth()
    {
        var factory = new ScriptLanguageFactory(() => [], () => []);

        var vs = factory.Create(ScriptLanguageFactory.VapourSynth);
        var avs = factory.Create(ScriptLanguageFactory.AviSynth);
        var missing = factory.Create("missing");

        Assert.True(factory.IsEnabled);
        Assert.NotNull(vs);
        Assert.NotNull(avs);
        Assert.Null(missing);
    }

    [Fact]
    public async Task IsEnabled_Disabled_SkipsEnumerationUntilEnabled()
    {
        var count = 0;
        var catalog = new CatalogCache(() =>
        {
            Interlocked.Increment(ref count);
            return [];
        });
        var factory = new ScriptLanguageFactory(
            [new("one", new VapourSynthLanguage(), catalog)])
        {
            IsEnabled = false
        };
        factory.Configure("one", "a");
        var disabled = factory.Create("one");
        var skipped = count;
        factory.IsEnabled = true;
        factory.Configure("one", "a");

        await catalog.GetAsync(CancellationToken.None);

        Assert.Null(disabled);
        Assert.Equal(0, skipped);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task IsEnabled_Disabled_SkipsRetainedServiceEnumeration()
    {
        const string text = "clip";
        var count = 0;
        var catalog = new CatalogCache(() =>
        {
            Interlocked.Increment(ref count);
            return [];
        });
        var factory = new ScriptLanguageFactory(
            [new("one", new VapourSynthLanguage(), catalog)]);
        var service = factory.Create("one")!;
        factory.IsEnabled = false;

        var reply = await service.GetAsync(text, text.Length, CancellationToken.None);

        Assert.Empty(reply.Items);
        Assert.Equal(0, count);
    }

    [Fact]
    public void Create_UnknownLanguage_ReturnsNull()
    {
        var factory = new ScriptLanguageFactory(
            [new("one", new VapourSynthLanguage(), new CatalogCache(() => []))]);

        var service = factory.Create("missing");

        Assert.Null(service);
    }

    [Fact]
    public void Create_KnownLanguage_ReturnsService()
    {
        var factory = new ScriptLanguageFactory(
            [new("one", new VapourSynthLanguage(), new CatalogCache(() => []))]);

        var service = factory.Create("one");

        Assert.NotNull(service);
    }

    [Fact]
    public void Configure_UnknownLanguage_DoesNotThrow()
    {
        var factory = new ScriptLanguageFactory([]);

        var exception = Record.Exception(() => factory.Configure("missing", "key"));

        Assert.Null(exception);
    }

    [Fact]
    public void Create_DuplicateLanguageId_Throws()
    {
        var language = new VapourSynthLanguage();
        var catalog = new CatalogCache(() => []);

        Assert.Throws<ArgumentException>(() => new ScriptLanguageFactory(
        [
            new("one", language, catalog),
            new("one", language, catalog)
        ]));
    }

    [Fact]
    public async Task GetAsync_EnumerationFailure_IsCached()
    {
        var count = 0;
        var cache = new CatalogCache(() =>
        {
            Interlocked.Increment(ref count);
            throw new InvalidOperationException();
        });
        cache.Refresh("path1");
        var service = new LanguageService(new VapourSynthLanguage(), cache);

        for (var i = 0; i < 5; i++)
        {
            Assert.Contains((await service.GetAsync("im", 2, CancellationToken.None)).Items,
                x => x.InsertionText == "import");
            cache.Refresh("path1");
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetAsync_EnumerationFailure_RerunsOnPathChange()
    {
        var count = 0;
        var cache = new CatalogCache(() =>
        {
            Interlocked.Increment(ref count);
            throw new InvalidOperationException();
        });
        cache.Refresh("path1");
        await cache.GetAsync(CancellationToken.None);

        cache.Refresh("path2");
        await cache.GetAsync(CancellationToken.None);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetAsync_EnumerationFailure_RerunsOnForceRefresh()
    {
        var count = 0;
        var cache = new CatalogCache(() =>
        {
            Interlocked.Increment(ref count);
            throw new InvalidOperationException();
        });
        cache.Refresh("path2");
        await cache.GetAsync(CancellationToken.None);

        cache.Refresh("path2", true);
        await cache.GetAsync(CancellationToken.None);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Refresh_ReusedCatalogList_SnapshotsNewSymbols()
    {
        const string text = "core.std.";
        var symbols = new List<Symbol>
        {
            new("core.std.Before", ["clip:vnode"], ReturnType: "clip:vnode;")
        };
        var factory = new ScriptLanguageFactory(() => symbols, () => []);
        var service = factory.Create(ScriptLanguageFactory.VapourSynth)!;
        var first = await service.GetAsync(text, text.Length, CancellationToken.None);
        Assert.Contains(first.Items, x => x.InsertionText == "Before");
        symbols.Clear();
        symbols.Add(new("core.std.After", ["clip:vnode"], ReturnType: "clip:vnode;"));

        factory.Refresh();

        var second = await service.GetAsync(text, text.Length, CancellationToken.None);
        Assert.Contains(second.Items, x => x.InsertionText == "After");
        Assert.DoesNotContain(second.Items, x => x.InsertionText == "Before");
    }

    [Fact]
    public void Configure_NewKey_InvalidatesIncludeCache()
    {
        const string text = "import helper as h\nh.";
        var current = "def Old():\n    return 1\n";
        var language = new VapourSynthLanguage(Read);
        var catalog = new CatalogCache(() => Array.Empty<Symbol>());
        var factory = new ScriptLanguageFactory([new("vs", language, catalog)]);
        var service = (LanguageService)factory.Create("vs")!;
        var native = Array.Empty<Symbol>();
        Assert.Contains(service.Analyze(text, text.Length, native).Items, x => x.InsertionText == "Old");
        current = "def New():\n    return 1\n";
        IncludeFile? Read(string specifier, string? _) => new IncludeFile("/plugins/helper.py", current);

        factory.Configure("vs", "other");

        var reply = service.Analyze(text, text.Length, native);
        Assert.Contains(reply.Items, x => x.InsertionText == "New");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Old");
    }

    [Fact]
    public async Task Refresh_ThenConfigureSameKey_DoesNotEnumerateAgain()
    {
        const string text = "im";
        var count = 0;
        var catalog = new CatalogCache(() =>
        {
            Interlocked.Increment(ref count);
            return [];
        });
        var language = new CountingLanguage(new VapourSynthLanguage());
        var factory = new ScriptLanguageFactory([new("one", language, catalog)]);
        factory.Configure("one", "A");
        var service = (LanguageService)factory.Create("one")!;
        await service.GetAsync(text, text.Length, CancellationToken.None);
        var binds = language.Binds;
        Assert.Equal(1, count);

        factory.Refresh();
        await service.GetAsync(text, text.Length, CancellationToken.None);
        factory.Configure("one", "A");
        await service.GetAsync(text, text.Length, CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Equal(binds + 1, language.Binds);
    }
}
