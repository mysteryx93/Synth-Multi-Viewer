# ScriptAssist

Completion, call insight, and hover for **VapourSynth** and **AviSynth** in [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit).

It knows the hosted object model (`vs`, `core`, `VideoNode`, AviSynth `last` and clip-taking filters). It is not a Python language server, not an AviSynth interpreter, and it never evaluates the buffer or loads a native core.

Package: `HanumanInstitute.ScriptAssist` (this project, `net10.0`, MIT, not on NuGet). How the library is laid out: [AGENTS.md](AGENTS.md).

```xml
<ProjectReference Include="path/to/ScriptAssist/ScriptAssist.csproj" />
```

Depends on `Avalonia.AvaloniaEdit` and `HanumanInstitute.Validators`. Import `HanumanInstitute.ScriptAssist` and `HanumanInstitute.ScriptAssist.AvaloniaEdit`. Do not global-using the AviSynth or VapourSynth namespaces.

## Host it

The factory already owns both languages. This assembly does not reference `ApiVapourSynth` / `ApiAviSynth`, so it never loads a native core. Catalogs come from those API projects (`VsCatalog.Read` / `AvsCatalog.Read`), not from the host app; you map them to `Symbol` and pass the funcs. Disk reads for `Import` / `import` stay outside the library because search paths are yours (`IncludeReader`).

```csharp
IReadOnlyList<Symbol> VsSymbols() => /* VsCatalog.Read() → core.ns.Name */;
IReadOnlyList<Symbol> AvsSymbols() => /* AvsCatalog.Read() → bare names */;

var factory = new ScriptLanguageFactory(VsSymbols, AvsSymbols, ReadPython, ReadAviSynth);
factory.Configure(ScriptLanguageFactory.VapourSynth, vsKey);
factory.Configure(ScriptLanguageFactory.AviSynth, avsKey);

var assist = new EditorAssist(editor, factory, () => ScriptLanguageFactory.VapourSynth, () => openPath);
assist.Attach();
```

`Create` / `Configure` ids are `ScriptLanguageFactory.VapourSynth` and `.AviSynth` (`"VapourSynth"` / `"AviSynth"`). Unknown ids: `Create` returns null, `Configure` is a no-op.

`IsEnabled` defaults to true. Set it false to skip completion, insight, hover, and catalog enumeration. `Configure` while disabled only remembers the key (`SetKey`); enumeration starts again once it is enabled.

Call `Configure` when the library path or plugin folders change. The key is opaque; a NUL-joined path works. `Refresh()` (Ctrl+Shift+R) re-enumerates. Pass `documentPath` so relative includes resolve; unsaved buffers only see the injected catalog.

There is no Splat helper. Register `IScriptLanguageFactory` yourself if you use a locator.

Headless (no UI):

```csharp
var service = factory.Create(ScriptLanguageFactory.VapourSynth)!;
Reply reply = await service.GetAsync(text, caret, cancellationToken, documentPath);
```

Discard the reply if the document or caret changed.

## Catalogs

`CatalogCache` enumerates once per key on a background task. Exceptions become an empty catalog.

**VapourSynth** names are `core.<namespace>.<Function>`. Arguments are the native semicolon-separated strings. Return type is the API4 string (often `clip:vnode;`). Two or more return keys stay untyped.

```csharp
new Symbol("core.std.Crop", ["clip:vnode", "left:int:opt"], ReturnType: "clip:vnode;")
```

**AviSynth** names are bare filters. Decode `$Plugin!Name!Param$` with `AviSynthParameters.Parse`, or take headers from parsed `function` lines. Merge autoload plugins with plugin-folder scripts via `AviSynthFunctions.UnionByName` (parsed headers replace native only when native has no named parameters).

```csharp
new Symbol("Crop", AviSynthParameters.Parse("c[left]i[top]i"));
```

## Includes

The library never reads disk. Pass an `IncludeReader`:

```csharp
IncludeFile? ReadAviSynth(string specifier, string? fromPath) =>
    ScriptFiles.AviSynth(specifier, fromPath, pluginDirectories, File.ReadAllText);

IncludeFile? ReadPython(string specifier, string? fromPath) =>
    ScriptFiles.PythonModule(specifier, fromPath, pluginAndSitePackageDirs, File.ReadAllText);
```

AviSynth looks for `.avs` / `.avsi` beside the script and in plugin folders. VapourSynth looks for `name.py` and `name/__init__.py` beside the script, in plugin folders, and in Python site-packages. Nested `Import` / `import` / `from` (including `from *` and `from .mod`) follow a visited-set of resolved paths. `import os` stays silent. Native `core.ns.Func` still comes only from the catalog you injected.

## Not in scope

Evaluating scripts, GScript `if`/`Eval` as scopes, running Python, stdlib/numpy, `__all__`, array/`vnode[]` as a distinct type, map keys, nested `FrameEval` defs, Python `class` bodies in imported modules, `f.props`.

## Tests

```bash
dotnet build ScriptAssist.Tests/ScriptAssist.Tests.csproj
dotnet ScriptAssist.Tests/bin/Debug/net10.0/ScriptAssist.Tests.dll
```

xunit.v3 **executable**. Do not use `dotnet test` / VSTest on .NET 10 for this project.
