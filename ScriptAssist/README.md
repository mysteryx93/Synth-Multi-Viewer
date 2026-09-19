# ScriptAssist

Completion, call insight, and hover for **VapourSynth** and **AviSynth**, with an [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) integration and a headless API.

ScriptAssist analyzes text and supplied catalogs. It requires no Python runtime, never executes scripts, and never loads a native core itself.

Targets .NET 10. Uses `Avalonia.AvaloniaEdit` and `HanumanInstitute.Validators`. Add a project reference:

```xml
<ProjectReference Include="path/to/ScriptAssist/ScriptAssist.csproj" />
```

## Attach to an editor

Supply two catalog callbacks returning `IReadOnlyList<Symbol>` (see [Catalogs](#catalogs)). The factory provides both languages; include readers are optional.

```csharp
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;

var factory = new ScriptLanguageFactory(ReadVsCatalog, ReadAvsCatalog);

var assist = new EditorAssist(editor, factory,
    () => ScriptLanguageFactory.VapourSynth,
    () => documentPath);
assist.Attach();
```

Keep `assist` for the editor's lifetime and call `Dispose()` when finished. The callbacks can return the current language and file path when an editor switches documents. Use `ScriptLanguageFactory.AviSynth` for AviSynth.

`EditorAssistOptions.Hint` and `Hover` set wrap width, line count, and character cap (`AssistTipSize`). Hover defaults are wider than the completion side panel and also wrap call-insight headers. Function signatures omit the `core.ns.` prefix.

Shortcuts: **Ctrl+Space** completion, **Ctrl+Shift+Space** call insight, **Ctrl+Shift+R** catalog refresh, **Escape** dismiss.

## Use without an editor

```csharp
var service = factory.Create(ScriptLanguageFactory.VapourSynth)!;
Reply reply = await service.GetAsync(text, caret, cancellationToken, documentPath);
```

`Reply` contains completion items, call insight, and hover text. Discard it if the document, caret, language, or path changed while awaiting it. `Create` returns null while `IsEnabled` is false. A service obtained earlier also skips catalog enumeration on `GetAsync` while the factory remains disabled.

## Catalog lifecycle

Catalogs load on first request. Call `factory.Configure(language, catalogKey)` to prefetch or when native library/plugin settings change; choose a key representing those settings. `factory.Refresh()` forces enumeration again. Catalog callbacks run in the background; exceptions produce an empty catalog.

`factory.IsEnabled` defaults to true. When false, `EditorAssist` skips requests, `Configure` only remembers the key, and `Refresh` does nothing. After re-enabling, the next request loads any pending catalog.

## Catalogs

The host maps native catalog entries to `Symbol`. This repository uses `VsCatalog.Read` / `AvsCatalog.Read` from the separate API projects; [ScriptCatalogs.cs](../SynthMultiViewer/Services/ScriptCatalogs.cs) shows the mapping.

**VapourSynth:** use `core.<namespace>.<Function>`, one native argument descriptor per array entry, and the API4 return string. Multiple return keys remain untyped.

```csharp
new Symbol("core.std.Crop", ["clip:vnode", "left:int:opt"], ReturnType: "clip:vnode;");
```

**AviSynth:** use bare function names. `AviSynthParameters.Parse` (in `HanumanInstitute.ScriptAssist.AviSynth`) decodes the native parameter format:

```csharp
new Symbol("Crop", AviSynthParameters.Parse("c[left]i[top]i"));
```

## Includes

Pass optional readers to follow external scripts. Each returns an `IncludeFile` with its resolved full path and text, or `null` when unavailable. The host owns search directories and disk access.

```csharp
IncludeFile? ReadAviSynth(string specifier, string? fromPath) =>
    ScriptFiles.AviSynth(specifier, fromPath, pluginDirectories, TryRead);

IncludeFile? ReadPython(string specifier, string? fromPath) =>
    ScriptFiles.PythonModule(specifier, fromPath, pythonDirectories, TryRead);

static string? TryRead(string path)
{
    try { return File.ReadAllText(path); }
    catch (IOException) { return null; }
    catch (UnauthorizedAccessException) { return null; }
}

var factory = new ScriptLanguageFactory(
    ReadVsCatalog, ReadAvsCatalog, ReadPython, ReadAviSynth);
```

`ScriptFiles` uses absolute paths directly; otherwise it tries paths beside `fromPath`, then the supplied directories. The per-path reader must return `null` for a missing or unreadable candidate so later paths are tried; do not pass `File.ReadAllText` directly. AviSynth uses the supplied filename, including its extension; Python tries `name.py` and `name/__init__.py`. Leading-dot Python imports resolve relative to the importing file. Include plugin or site-packages directories in the host's roots as needed.

Pass the open document's path for sibling imports. Unsaved buffers can still use supplied search roots. Autoload AviSynth scripts can be parsed with `AviSynthFunctions.Parse` and merged into the host catalog with `UnionByName`.

Imported exports and failed lookups are cached on the language (bounded LRU). The current bind keeps its own import graph, and that document's working set is pinned so a later edit of the same file does not cascade-reread. `Invalidate`, factory `Refresh`, or `Configure` with a new catalog key still drop the cache. Files that fall out of the cache and the working set are read again.

## Scope

Type inference and import parsing are partial. Supported assistance includes core/node members, catalog signatures, local bindings, and imported script function headers. This is not a full Python or AviSynth interpreter.

Outside scope: stdlib/numpy analysis, `__all__`, imported Python class members, array/list element types, map keys, `f.props`, GScript/Eval scopes, and nested FrameEval function environments.

## Tests

Run from the repository root:

```bash
dotnet build ScriptAssist.Tests/ScriptAssist.Tests.csproj
dotnet ScriptAssist.Tests/bin/Debug/net10.0/ScriptAssist.Tests.dll
```

The tests use the xunit.v3 executable runner, rather than `dotnet test` / VSTest. Implementation notes: [AGENTS.md](AGENTS.md). Repository test contract: [../AGENTS.md](../AGENTS.md).
