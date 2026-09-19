# Functions Explorer

A modeless function browser that is a **ScriptAssist feature** with a thin host window. Completion, hover, and this list share catalogs, bind, and module-export parse. The host does not walk plugins, parse scripts, or invent groups.

## Why

vsedit-style browsing was first sketched as a host overlay on `CatalogCache`. That would have duplicated native enumeration, header parse, and grouping, and would have grown a VapourSynth-only module tree next to a flat AviSynth list.

Functions Explorer is how you look at what ScriptAssist already knows, plus one browse-only pass for **installed but not yet imported** VapourSynth script packages, using the same export parser bind uses after `import`. AviSynth has no package layer; its explorer is Internal / Plugin / Autoload / This file from the catalog the editor already completes.

The window lives in Synth Multi-Viewer (Video Properties lifecycle). The data plane lives in ScriptAssist. Native cores stay in ApiAviSynth / ApiVapourSynth.

## Rules for this feature

1. A type does one job. Native dump, `Symbol` mapping, autoload-script merge, snapshot bind, and the window are separate types. `VapourSynthBinder` stays the VS bind; do not explode scanners into services.
2. Inputs and outputs at process/disk boundaries are named types (native catalog dumps, `IScriptDirectory`, `IIncludeSource`). Tests mock those. Hover/completion tests still pass **data** (script text, `Symbol` lists).
3. Production types exist because the host and the language engine need them. Do not add constructors, `Func` catalogs, or `AllowRequests` lambdas so a test can pass `() => []`.

## Boundary

**ScriptAssist owns** language grouping, snapshot bind, browse projection, installed-Python package listing from **host-supplied roots**, module export parse (shared with `import`), namespace hover/hint text, and the refresh contract.

**The app owns** calling `VsCatalog.Read` / `AvsCatalog.Read`, plugin/site-package **directories**, plugin-folder disk, the modeless window, toolbar/shortcut, insert at caret, and wiring `ScriptAssistService`.

**ApiVapourSynth / ApiAviSynth own** throwaway-core enumeration and extra native fields (plugin title/id/path/version; AVS `$InternalFunctions$` bucket). They do not group or browse.

**ScriptAssist never** references the app, loads a native core, or evaluates scripts. **AvaloniaEdit in the library stays** attach / debounce / popups. Do not put `FunctionsExplorerView` in the library.

```
App window  →  factory.BrowseAsync / Refresh
                    │
                    ├─ CatalogCache.GetAsync      (ISymbolSource, once per config)
                    ├─ LanguageService snapshot     (same LRU as GetAsync)
                    ├─ VS: package index from roots (browse only, not Analyze)
                    └─ ILanguage.Browse             (groups + insert text)
```

`Analyze` / `GetAsync` stay caret-relative completion. They do **not** load unimported packages. `Members()` does **not** dump the installed-module index.

## Production collaborators (not lambdas)

| Type | Job |
|---|---|
| `IVapourSynthNativeCatalog` | Copy API4 filter rows (no `Symbol`) |
| `IAviSynthNativeCatalog` | Copy autoload filter rows (no `Symbol`) |
| `IScriptDirectory` | Plugin-folder roots, file names, and text |
| `IIncludeSource` | Resolve an import specifier to path+text, or null |
| `ISymbolSource` | Mapped catalog for `CatalogCache` (factory-built, not a host ctor argument) |
| `IAssistSession` | Current language, path, factory for the attached editor |
| `VapourSynthSymbolSource` | Map native VS rows → `Symbol` (Title on plugin) |
| `AviSynthSymbolSource` | Map native AVS rows → `Symbol.Group` + merge autoload `.avsi` via `Parse` / `UnionByName` |
| `CatalogCache` | One background task per config key around an `ISymbolSource` |
| `ScriptLanguageFactory` | VS + AVS profiles only; no profile-list constructor |

The factory constructor is:

```csharp
public ScriptLanguageFactory(
    IVapourSynthNativeCatalog vapoursynth,
    IAviSynthNativeCatalog avisynth,
    IScriptDirectory avisynthAutoload,
    IIncludeSource? vapoursynthIncludes = null,
    IIncludeSource? avisynthIncludes = null)
```

`Refresh()` always force-enumerates and invalidates snapshots/includes, **including when `IsEnabled` is false**. `Create` returns null while disabled; `EditorAssist` skips requests. Explorer and `Ctrl+Shift+R` call the same `Refresh()`, then `BrowseAsync` if the window is open.

`IsEnabled` lives on `IScriptLanguageFactory`. `LanguageService` does not take a one-bool gate.

## Native fields

**ApiVapourSynth:** bind `GetPluginName` (and id/path/version for explorer detail). `VsFilterInfo` carries `PluginName`. `VapourSynthSymbolSource` sets `Symbol.Title`.

**ApiAviSynth:** `AvsFilterInfo.Category` is the `$InternalFunctions$` / `$PluginFunctions$` / `$UserFunctions$` bucket the reader already has.

**Hover / completion hint** for `core.<ns>` / `clip.<ns>`: one short string, still matching each other — **title if present, else `"plugin"`**. Do not repeat the namespace id. Path/id/version stay on the explorer row, not in the editor tip. AviSynth hover stays the function signature.

## Browse API

```csharp
public sealed record BrowseGroup(string Name, IReadOnlyList<BrowseFunction> Functions);
public sealed record BrowseFunction(string Name, string Signature, string InsertText);
```

`ILanguage.Browse(catalog, bindings)` (later: extra installed modules not in `ScriptModules`).

`ILanguageService.BrowseAsync(text, token, documentPath)` — `GetAsync` catalog, same snapshot as `Analyze`, then `Browse`. **Does not honor the assist gate.** Disabled assist must not empty the explorer.

**VapourSynth groups:** native `core.<ns>.*` → group `<ns>`, insert `core.<ns>.Func()`; imported packages → group = alias; This file → buffer `def`s, insert `Name()`. Plugins and Python packages are siblings. Skip `vs` / `vapoursynth` script modules.

**AviSynth groups:** Internal / Plugin / Autoload (`User` labeled Autoload) from `Symbol.Group`; This file from `BufferSymbols`. Insert `Name()`.

This file is a snapshot at open and on Refresh, not on keystroke.

### Installed Python packages (browse only, later slice)

Helper next to `ScriptFiles`: given roots, list import names from `.dist-info` / `.egg-info` (Requires-Dist or known `vsutil` / `mvsfunc` / `vsjetpack` / `havsfunc`) and loose root `*.py` with a vapoursynth mention (32 KB cap). Skip the `VapourSynth` binding dist, native-only RECORD, and `vsjetpack` meta when siblings exist. Do not follow Requires-Dist into numpy / jetpytools / rich.

`BrowseAsync` loads packages **not** already in `ScriptModules` through `IIncludeSource` + the shared export parse. **`Bind` / `GetAsync` do not call this.**

### Shared module export surface (later slice)

Extract the export walk `FillModule` already does. Column-0 `class Name` in an imported / listed package becomes an insertable export (name only). Nested methods stay out.

## Host UI (later slice)

Name: **Functions Explorer**. Modeless, Video Properties pattern. Toolbar toggle on editors only (`IsEditorSelected`). Shortcut `Ctrl+E`. About Editor: `Ctrl+E: Functions explorer`. `Ctrl+Shift+R` remains Refresh catalogs.

Search + two lists (groups | functions). Refresh button = `factory.Refresh()` + `BrowseAsync`. Insert canonical call at caret, caret inside `()`. Viewer tab: keep last list, disable Insert.

No live `Document.Changed` rebrowse. Filter in memory only.

## What does not change

- Unimported Python names do not appear in completion until the buffer imports them.
- Locals, keywords, `VideoNode` properties stay out of the explorer.
- No docking. No second `VsCatalog.Read` from the window.
- No `__all__` product, no class-member completion, no AviSynth per-DLL names.

## Implementation order

1. **Ports and mapping (this change)** — interfaces, factory, move `ScriptCatalogs` into ScriptAssist, Category/Title, hover title, Refresh when disabled, tests mock I/O ports.
2. **Browse + export parse + package index** — `Browse*` types, `BrowseAsync`, Python inventory from roots, class-name exports.
3. **Host window** — view/view-model, toolbar, Ctrl+E, insert, icon.

## Tests

Lock observable behavior. No visual-tree tests.

- Mapper/merge: mock native dump + files; assert `Symbol.Group` / `Title` / `UnionByName`.
- Factory: mock native catalogs; `Refresh` when disabled still enumerates; `Create` when disabled returns null.
- Hover: namespace with `Title` matches completion hint; without title stays `"plugin"`.
- Browse (slice 2): groups, This file, import alias, unimported package browse-only, `GetAsync` does not complete unimported exports.
- Host: toggle, insert at caret, Help `Ctrl+E`. Fake factory. No `AvaloniaFact` for markup.

Do not re-assert `Symbol.Signature` strings already locked by hover.
