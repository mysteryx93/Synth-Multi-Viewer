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
    public void Invalidate_IncludeSnapshots_RefreshesMembers()
    {
        const string text = "import helper as h\nh.";
        var current = "def Old():\n    return 1\n";
        var catalog = new CatalogCache(() => Array.Empty<Symbol>());
        var service = new LanguageService(new VapourSynthLanguage(Read), catalog);
        var native = Array.Empty<Symbol>();
        Assert.Contains(service.Analyze(text, text.Length, native).Items, x => x.InsertionText == "Old");
        current = "def New():\n    return 1\n";
        Assert.Contains(service.Analyze(text, text.Length, native).Items, x => x.InsertionText == "Old");
        IncludeFile? Read(string specifier, string? _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", current) : null;

        service.Invalidate();

        var refreshed = service.Analyze(text, text.Length, native);
        Assert.Contains(refreshed.Items, x => x.InsertionText == "New");
        Assert.DoesNotContain(refreshed.Items, x => x.InsertionText == "Old");
    }

    [Fact]
    public async Task Invalidate_StaleInFlightSnapshot_DoesNotPublish()
    {
        const string text = "import helper as h\nh.";
        var started = new ManualResetEventSlim(false);
        var proceed = new ManualResetEventSlim(false);
        var current = "def Old():\n    return 1\n";
        var service = new LanguageService(new VapourSynthLanguage(Read), new CatalogCache(() => []));
        var native = Array.Empty<Symbol>();
        var first = Task.Run(() => service.Analyze(text, text.Length, native));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        current = "def New():\n    return 1\n";
        IncludeFile? Read(string specifier, string? _)
        {
            if (specifier != "helper")
            {
                return null;
            }
            var file = new IncludeFile("/plugins/helper.py", current);
            started.Set();
            proceed.Wait();
            return file;
        }

        service.Invalidate();
        proceed.Set();
        await first;

        var next = service.Analyze(text, text.Length, native);
        Assert.Contains(next.Items, x => x.InsertionText == "New");
        Assert.DoesNotContain(next.Items, x => x.InsertionText == "Old");
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
    public void Refresh_RepeatedFromImport_ReadsHelperOnce()
    {
        var reads = 0;
        var helper = string.Concat(Enumerable.Range(0, 30)
            .Select(i => $"def Filter{i}(clip) -> vs.VideoNode:\n    return clip\n"));
        var service = VsService(Read);
        var text = string.Concat(Enumerable.Range(0, 30).Select(i => $"from helper import Filter{i}\n")) +
            "core.std.Crop(";
        IncludeFile? Read(string specifier, string? _)
        {
            reads++;
            return specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;
        }

        service.Analyze(text, text.Length, Vs);

        Assert.Equal(1, reads);
    }

    [Fact]
    public void Refresh_RepeatedFromImport_DoesNotRereadUntilInvalidate()
    {
        var reads = 0;
        var helper = string.Concat(Enumerable.Range(0, 30)
            .Select(i => $"def Filter{i}(clip) -> vs.VideoNode:\n    return clip\n"));
        var service = VsService(Read);
        var text = string.Concat(Enumerable.Range(0, 30).Select(i => $"from helper import Filter{i}\n")) +
            "core.std.Crop(";
        service.Analyze(text, text.Length, Vs);
        reads = 0;
        IncludeFile? Read(string specifier, string? _)
        {
            reads++;
            return specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;
        }

        service.Analyze(text + "\n", text.Length, Vs);

        Assert.Equal(0, reads);
    }

    [Fact]
    public void Refresh_RepeatedFromImport_RereadsAfterInvalidate()
    {
        var reads = 0;
        var helper = string.Concat(Enumerable.Range(0, 30)
            .Select(i => $"def Filter{i}(clip) -> vs.VideoNode:\n    return clip\n"));
        var service = VsService(Read);
        var text = string.Concat(Enumerable.Range(0, 30).Select(i => $"from helper import Filter{i}\n")) +
            "core.std.Crop(";
        service.Analyze(text, text.Length, Vs);
        reads = 0;
        service.Analyze(text + "\n", text.Length, Vs);
        service.Invalidate();
        IncludeFile? Read(string specifier, string? _)
        {
            reads++;
            return specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;
        }

        service.Analyze(text, text.Length, Vs);

        Assert.Equal(1, reads);
    }

    [Fact]
    public void GetAsync_TwoDocuments_KeepsSnapshotCache()
    {
        const string a = "def one():\n    pass\ncore.ns.F0(";
        const string b = "def two():\n    pass\ncore.ns.F0(";
        var catalog = Enumerable.Range(0, 40)
            .Select(i => new Symbol("core.ns.F" + i, ["clip:vnode"], ReturnType: "clip:vnode;"))
            .ToArray();
        var language = new CountingLanguage(new VapourSynthLanguage());
        var service = new LanguageService(language, new CatalogCache(() => catalog));
        Assert.NotNull(service.Analyze(a, a.Length, catalog).Insight);
        Assert.NotNull(service.Analyze(b, b.Length, catalog).Insight);
        var binds = language.Binds;
        Assert.Equal(2, binds);

        var secondA = service.Analyze(a, a.Length, catalog);
        var secondB = service.Analyze(b, b.Length, catalog);

        Assert.NotNull(secondA.Insight);
        Assert.NotNull(secondB.Insight);
        Assert.Equal(binds, language.Binds);
    }

    [Fact]
    public async Task GetAsync_ParallelBinds_SurvivesIncludeCache()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        const string text = "from helper import Filter\nFilter(";
        var service = VsService(Read);
        var tasks = Enumerable.Range(0, 24)
            .Select(_ => Task.Run(() => service.Analyze(text, text.Length, Vs)));
        IncludeFile? Read(string specifier, string? _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null;

        var results = await Task.WhenAll(tasks);

        Assert.All(results, reply =>
        {
            Assert.NotNull(reply.Insight);
            Assert.Equal("Filter", reply.Insight.Overloads[0].Name);
        });
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

    [Fact]
    public async Task GetAsync_ParallelSameSnapshot_BindsOnce()
    {
        const string text = "core.std.BlankClip(";
        var started = new ManualResetEventSlim(false);
        var proceed = new ManualResetEventSlim(false);
        var catalog = new[] { new Symbol("core.std.BlankClip", ["clip:vnode"], ReturnType: "clip:vnode;") };
        var language = new CountingLanguage(new VapourSynthLanguage(), started, proceed);
        var service = new LanguageService(language, new CatalogCache(() => catalog));
        var first = Task.Run(() => service.Analyze(text, text.Length, catalog));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        var second = Task.Run(() => service.Analyze(text, text.Length, catalog));
        var duplicated = SpinWait.SpinUntil(() => Volatile.Read(ref language.Binds) > 1, 250);

        proceed.Set();
        var results = await Task.WhenAll(first, second);

        Assert.False(duplicated);
        Assert.Equal(1, language.Binds);
        Assert.All(results, reply => Assert.NotNull(reply.Insight));
    }

    [Fact]
    public void IncludeCache_ExceedsEntryLimit_EvictsOldest()
    {
        var cache = new IncludeCache();
        var session = new IncludeSession(cache);
        for (var i = 0; i < 80; i++)
        {
            session.SetEntry("/plugins/f" + i + ".py", new IncludeEntry([], []));
        }

        Assert.False(cache.TryEntry("/plugins/f0.py", out _));
        Assert.True(cache.TryEntry("/plugins/f79.py", out _));
    }

    [Fact]
    public async Task GetAsync_CancelledFirstCaller_DoesNotWaitForBuild()
    {
        const string text = "import helper as h\nh.";
        var started = new ManualResetEventSlim(false);
        var proceed = new ManualResetEventSlim(false);
        var service = new LanguageService(new VapourSynthLanguage(Read), new CatalogCache(() => []));
        var native = Array.Empty<Symbol>();
        using var cts = new CancellationTokenSource();
        var first = Task.Run(() => service.Analyze(text, text.Length, native, cts.Token));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        cts.Cancel();
        IncludeFile? Read(string specifier, string? _)
        {
            if (specifier != "helper")
            {
                return null;
            }

            started.Set();
            proceed.Wait();
            return new IncludeFile("/plugins/helper.py", "def Old():\n    return 1\n");
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        proceed.Set();
    }

    [Fact]
    public async Task Invalidate_OverlappingRequest_DoesNotReuseInflightSnapshot()
    {
        const string text = "import helper as h\nh.";
        var started = new ManualResetEventSlim(false);
        var proceed = new ManualResetEventSlim(false);
        var current = "def Old():\n    return 1\n";
        var reads = 0;
        var service = new LanguageService(new VapourSynthLanguage(Read), new CatalogCache(() => []));
        var native = Array.Empty<Symbol>();
        var first = Task.Run(() => service.Analyze(text, text.Length, native));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        current = "def New():\n    return 1\n";
        IncludeFile? Read(string specifier, string? _)
        {
            if (specifier != "helper")
            {
                return null;
            }

            Interlocked.Increment(ref reads);
            started.Set();
            proceed.Wait();
            return new IncludeFile("/plugins/helper.py", current);
        }

        service.Invalidate();
        var second = Task.Run(() => service.Analyze(text, text.Length, native));
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref reads) >= 2, 2000));
        proceed.Set();
        var results = await Task.WhenAll(first, second);

        Assert.Contains(results[1].Items, x => x.InsertionText == "New");
        Assert.DoesNotContain(results[1].Items, x => x.InsertionText == "Old");
    }

    private sealed class CountingLanguage(ILanguage inner, ManualResetEventSlim? started = null,
        ManualResetEventSlim? proceed = null) : ILanguage
    {
        public int Binds;

        public LexerOptions Lexer => inner.Lexer;
        public StringComparison Comparison => inner.Comparison;
        public IReadOnlyList<Symbol> Keywords => inner.Keywords;

        public DocumentBindings Bind(string text, IReadOnlyList<Symbol> catalog, CancellationToken token,
            string? documentPath = null)
        {
            Interlocked.Increment(ref Binds);
            started?.Set();
            proceed?.Wait();
            return inner.Bind(text, catalog, token, documentPath);
        }

        public TypeRef TypeOf(IReadOnlyList<PathSegment> segments, DocumentBindings bindings,
            IReadOnlyList<Symbol> catalog) =>
            inner.TypeOf(segments, bindings, catalog);

        public IReadOnlyList<Symbol> Members(TypeRef type, IReadOnlyList<Symbol> catalog, DocumentBindings bindings) =>
            inner.Members(type, catalog, bindings);

        public CallResolution? ResolveCall(IReadOnlyList<PathSegment> callee, DocumentBindings bindings,
            IReadOnlyList<Symbol> catalog) =>
            inner.ResolveCall(callee, bindings, catalog);

        public HoverInfo? Hover(string code, CaretPath path, DocumentBindings bindings, IReadOnlyList<Symbol> catalog) =>
            inner.Hover(code, path, bindings, catalog);

        public double CompletionPriority(Symbol symbol, TypeRef receiver) =>
            inner.CompletionPriority(symbol, receiver);

        public string? ParameterName(string parameter) => inner.ParameterName(parameter);
    }
}
