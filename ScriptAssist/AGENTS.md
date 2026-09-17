# ScriptAssist — agent notes

How this library is laid out, what must stay true, and where to change it. User-facing API and host wiring: [README.md](README.md).

This is **one assembly**. Do not split packages. Namespaces stay split-ready: engine, AviSynth, VapourSynth, and AvaloniaEdit do not share global usings.

## Non-goals

Not an LSP. Not a Python IDE. Not an AviSynth interpreter. The library **never** loads a native core and **never** references `ApiVapourSynth`, `ApiAviSynth`, or the app. Native catalogs live on those API types (`VsCatalog` / `AvsCatalog`); the app’s `ScriptCatalogs` is only the `Symbol` mapping plus plugin-folder `.avsi` parse. That mapping is not an app feature.

Do type/`clip.` work **here**, not in `SynthMultiViewer/Services/Completion`. The factory owns VapourSynth and AviSynth (`ScriptLanguageFactory.VapourSynth` / `.AviSynth`). Hosts inject catalogs and include readers, not `LanguageProfile` lists or invented ids. The profile-list constructor is for tests. Engine types (`LanguageService`, `CatalogCache`) stay language-agnostic.

Out of scope unless a task says otherwise: eval, GScript scopes, running Python, stdlib/numpy/`__all__`, array/list types and `vnode[]` as a distinct type, map keys, nested FrameEval defs, class wrappers in imported modules, `f.props`.

## Layout

| Location | Namespace | Visibility |
| --- | --- | --- |
| Project root: `ILanguage`, `ILanguageService`, `IScriptLanguageFactory`, `ISymbolCatalog`, `LanguageService`, `ScriptLanguageFactory`, `LanguageProfile`, `CatalogCache`, `ScriptFiles`, `Usings.cs` | `HanumanInstitute.ScriptAssist` | public |
| `Models/` | `HanumanInstitute.ScriptAssist` (do **not** change namespace when moving files) | public DTOs |
| `Utilities/` | `HanumanInstitute.ScriptAssist` | **internal** |
| `VapourSynth/` | `HanumanInstitute.ScriptAssist.VapourSynth` | public language + parsers; binders internal |
| `AviSynth/` | `HanumanInstitute.ScriptAssist.AviSynth` | public language + parsers; binders internal |
| `AvaloniaEdit/` | `HanumanInstitute.ScriptAssist.AvaloniaEdit` | public host only |

There is no `Engine/` folder. Models: `Symbol`, `SymbolKind`, `TypeRef`, `PathSegment`, `PathSegmentKind`, `CaretPath`, `DocumentBindings`, `BindingScope`, `LexerOptions`, `Reply`, `CompletionItem`, `CallInsight`, `CallResolution`, `HoverInfo`, `IncludeFile`, `LexedBuffer`.

`LanguageService.Analyze` is **internal**. Hosts use `GetAsync`. Tests use `InternalsVisibleTo` (`ScriptAssist.Tests`, `SynthMultiViewer.Tests`).

Static classes are intentional: lex/scan/split (`BufferLexer`, `ExpressionReader`, `CallScanner`, `ExpressionParts`, `ParameterNames`, `FunctionHeaders`, `NamedArgumentHover`, `IncludePaths`), type tables (`VapourSynthTypes` / `HostTypes`, `AviSynthTypes` / `Internals`), GeneratedRegex patterns, catalog parsers. Binders, type walkers, and member lists are internal file splits of a language, not hidden objects. Do not turn them into instance types or `Symbol` extension methods (that leaks language rules into the engine).

`ILanguage.CompletionPriority` ranks items. `LanguageService` must not special-case `vnode` / `clip`.

Lists on stored contracts are `IReadOnlyList`. Streaming path APIs (`IncludePaths`, folder enumeration) stay `IEnumerable`.

## Pipeline

```
GetAsync(text, caret, token, documentPath)
  catalog = ISymbolCatalog.GetAsync
  Snapshot: Mask full document once (version + path + catalog identity)
            Bind(original text, catalog, token, documentPath)
            merge BufferSymbols over native by name
  prefix = Mask(text[..caret])  if caret < Length else snapshot.Masked
  if prefix.InLiteral → empty Reply
  bindings = snapshot.Bindings.At(caret)   // overlay function scopes
  path = ExpressionReader.Read(masked, caret)
  insight = InFunctionHeader(caret) ? null : CallScanner.Find(prefix)
  items = Complete(path, TypeOf(path.Segments), …)
  hover = ILanguage.Hover(masked, path, bindings, catalog)
```

Snapshot is O(n), cached on document text + path + catalog reference. No incremental parser, no file-size cap. Honor `CancellationToken` in `Mask` and `CallScanner`.

`TypeRef.Root` is the **empty completion path** (statement start), not a value type. Never store `Root` in `DocumentBindings.Names`. Empty `ExpressionReader.Parse` on an assignment RHS is `Unknown`, not `Root`. Parenthesize unwrap (`ExpressionParts.UnwrapParentheses`) before `?:` / call parse so `(core.std.BlankClip())` stays a node and `(4, 8, 16)` does not hover as `root`.

### Hover rules (both languages)

- Locals and properties: **type only** (`VideoNode`, `clip`, `int`), never `name: type`.
- If the type text equals the identifier (`VideoNode`, a variable named `clip` of type clip), stay silent.
- Named `name=` inside a call is a parameter (`NamedArgumentHover`), even when the callee is unknown: consume the token, do not fall through to a same-named local.
- Function **parameter names** in the header: silent. Call insight is also skipped in headers (`InFunctionHeader`, including unclosed header at EOF when `HeaderEnd == End`).
- `vs` in `clip: vs.VideoNode` may hover (`vapoursynth`). The type name `VideoNode` must not.

### Completion hints

`CompletionData.HintText`: function parameter list (or `"No parameters"`); for `Property` / `Local`, the part after `:` in `Signature`. Wrap 560px / 8 lines, truncate 400 chars.

VS locals must use **display** return types (`VideoNode`, not `vnode`) so the side panel is not blank. Format constants (`FLOAT`, `YUV`, `GRAY8`) are `SymbolKind.Property` with `ReturnType: "int"` so they hint. `VideoNode` / `AudioNode` on `vs` stay `Namespace` (no tautological hint).

## Host contract

`ScriptLanguageFactory` owns VS+AVS. Constructor takes catalog funcs (mapped from `VsCatalog`/`AvsCatalog` by whoever references the APIs) plus optional `IncludeReader`s. It copies profiles into a dictionary (stored list, not `IEnumerable`). `IsEnabled` is a settable bool (default true). `Func<bool>?` lives only on `EditorAssistOptions`, pointed at `factory.IsEnabled`. The `EditorAssist(editor, factory, language, documentPath)` constructor wires that. Duplicate profile ids throw `ArgumentException`. The factory must not call `VsHelper.SetDllPath`; the host does that, then `Configure`s with a new key.

`ISymbolCatalog`: `SetKey` remembers without enumerating; `Refresh` enumerates; `GetAsync` waits without cancelling native work. `CatalogCache` swallows enumerator exceptions → empty list. One task per key.

`IncludeReader`: `(specifier, fromPath) => IncludeFile?`. Library does not touch disk. `ScriptFiles` / `IncludePaths` only build candidates. `ScriptFiles.Open` uses `Path.GetFullPath` on the first readable path.

`GetAsync` / `Bind` take `documentPath`. Snapshot cache key includes it. Buffer symbols replace same-named native entries.

AvaloniaEdit: `EditorAssist` attach/detach, debounce, stale-reply discard. Presenters own popups. Delays: 100 ms first letter, 0 ms on `.` `(` `,`, 40 ms caret while insight is open, ~300 ms hover. Ctrl+Space list, Ctrl+Shift+Space insight, Ctrl+Shift+R `RefreshCatalogs`, Escape dismisses.

`EditorAssist` may leak `Reply` to the host. Do not leak other engine types through the host API.

No Splat register helper in the library.

## Validators

`HanumanInstitute.Validators` (aligned with the app pin). Fluent `CheckNotNull` / `CheckNotNullOrEmpty`, `HasValue()`, `Clamp`, `FormatInvariant`.

`HasValue()` is `!IsNullOrEmpty` only. Do **not** replace `string.IsNullOrWhiteSpace` with `HasValue()`. Leave `Length == 0` on non-nullable strings.

## Lexer

`BufferLexer.Mask` blanks comments/strings in place (newlines kept). Closers: `*/` (`*`), `]/` (`]`), `*]` (`[`) for `[* *]`.

AviSynth `LexerOptions`: hash comments, `/* */`, `/[ ]/`, `[* *]`, triple quotes, doubled quotes.

VapourSynth: hash comments, single quotes, triple quotes, backslash escapes.

**AviSynth join order:** `JoinContinuations` **then** `Mask` (`AviSynthPatterns.Clean`). `\` is preprocessing; a leading `\` inside a later `"""` string still joins the outer statement (FrameRateConverter `GScriptClip("""…`)`). Same-length spaces, offsets stay UTF-16 document offsets.

## AviSynth

Files: `AviSynthLanguage`, `AviSynthBinder`, `AviSynthTypeWalker`, `AviSynthFunctions`, `AviSynthParameters`, `AviSynthPatterns`, `AviSynthInternals`, `AviSynthTypes`.

- Comparison: ordinal ignore case.
- `last` is always clip (global).
- Implicit first clip on unbound calls; insight strips it; `CallResolution.ImplicitReceiver` / `ImplicitClip`.
- Unknown **called** names default to clip (`AviSynthInternals.ReturnOf`) except the small internal table (`Width` → int, …). Clip properties without `()` go through `ReturnOf`, not Unknown.
- `function` parameters bind in `BindingScope` (`clip C` → clip). Script-level `Names`: `last`, top-level assigns, `global x =`. `DocumentBindings.At(caret)` overlays containing scopes.
- Do **not** bind imported AVSI parameters. `AddImports` / plugin-folder parse add **signatures** only. Open the AVSI to get its scopes.
- Header without `{` ends at the next `function`, not EOF. Unclosed `function Foo(clip C,` keeps the span (`ParenClose` = last index).
- Infer: unwrap parens; `Default(x, y)` keeps type of `x`; top-level `?:` prefers clip if either branch is clip.
- Last assignment wins. A later `R = Debug ? R.GScriptClip("""…""") : R` overwrites the earlier ternary.
- GScript `if` / `Eval` are not scopes.
- Type words in a header (`string "Preset"`) are not `String()`. `String(5)` still hovers as the function.
- `IndexOutsideBrackets` treats `()` `[]` `{}`. Unmasked `[` in `[** comment *]` used to steal `:` from ternaries — comments must be lexed.

Plugin-folder `*.avsi` / `*.avs`: host parses and `UnionByName`. Buffer `Import()` and those files both recurse with a visited-set of resolved paths.

Real scripts that must keep working: xClean trailing `\`, FrameRateConverter leading `\` and `[** *]` / `"""`.

## VapourSynth

Files: `VapourSynthLanguage`, `VapourSynthBinder`, `VapourSynthTypeWalker`, `VapourSynthMembers`, `VapourSynthHostTypes`, `VapourSynthTypes`, `VapourSynthFunctions`, `VapourSynthPatterns`, `VapourSynthCatalogIndex`, `VapourSynthArguments`.

- Comparison: ordinal.
- Catalog names `core.ns.Func`. Native `VsCatalog` (host) is the **only** `core.ns.Func` source.
- `FromReturn`: split `;`, ignore empty parts; **two or more keys → Unknown**. Tests must use native terminator form `clip:vnode;`.
- `clip.` is silent unless the receiver is core, a plugin namespace, a bound plugin, or a typed node. Do not dump Crop at statement root. After `clip = core.std.BlankClip()`, `clip.` offers `std` / `width`.
- Dotted assignment does not create locals.
- `+` / `*` / slice keep node type. `ExpressionReader` walks trailing `()` / `[]`. `[` is not `.` for members; `[` commits completion then stays quiet; insight inside `[]` belongs to the enclosing `(`.
- `TypeRef.Root` must not appear on tuples. Unwrap parens so `(core.std.BlankClip())` is a node.
- Literals: int, float, `True`/`False`, quoted strings (Infer the **unmasked** RHS via a `maskStrings: false` buffer at the same offsets).
- Function scopes: `AnyDef` (`^\s*def`) including class methods. Body while indent ≥ first body line; blank/comment/masked-string lines do not end the block. `TopLevelDef` (`^def`) is **only** for imported module `Parse` — do not dump indented methods as `haf.method`.
- Parameters: annotation (`clip: vs.VideoNode`, `radius: int`, `Optional[int]`, `int | None`), default (`radius=1`), name `clip` → VideoNode. `self` is not clip.
- Nested `def` gets its own scope; outer names still overlay via `At(caret)`.
- `LoadModule`: nested `import` / `from` / `from *`, relative `from .foo`. Cache by path. Failed imports must **not** `TryAdd` Unknown into `Names`. `HostMember` treats `ReturnType` that is `script:id` as a nested module, not a call.
- `VapourSynthHostTypes`: declare `Formats` / `Families` **before** `ModuleMembers` Select (cctor NRE).
- Display names live on `VapourSynthTypes.Display` / `DisplayReturn`. Hover and local `ReturnType` use those.

Python search path is a **host** concern (`ScriptIncludeIO` in the app): script dir + native plugin dirs + `GetPythonModuleDirectories` (site-packages). Native `GetSystemPluginDirectories` must **not** include site-packages (autoload `.so` would mix with `.py`). `havsfunc.py` lives in site-packages; `import havsfunc as haf` then completes that file’s column-0 defs. `ChangeFPS` is a real havsfunc utility, not an AviSynth leak.

`IncludePaths.PythonModule`: a leading-dot specifier is relative to the **loaded file** directory. Do not `Replace('.', sep)` a `.qtgmc` into a rooted `/qtgmc`.

## Includes and paths

`IncludePaths.AviSynth` / `PythonModule` candidate order: rooted specifier, beside `fromPath`, then roots.

Cycles: visited-set of resolved full paths.

Host document path: `IEditorViewModel.FileName` in this app. Unsaved buffers cannot see sibling scripts.

## What not to change without a product decision

- Do not add a full Python parser or type checker.
- Do not evaluate the buffer for types.
- Do not put VS `.py` defs at statement root until `import` / `from`.
- Do not guess every conventional parameter name (`Input`, `src`). `clip` is the one convention.
- Do not convert engine static helpers to services.
- Do not global-using language namespaces except at composition (`ScriptLanguageFactory`, `ScriptCatalogs`, `BindableTextEditor`).

## Tests

`ScriptAssist.Tests` is xunit.v3 **OutputType Exe**. On .NET 10, `dotnet test` / VSTest is rejected.

```bash
dotnet build ScriptAssist.Tests/ScriptAssist.Tests.csproj
dotnet ScriptAssist.Tests/bin/Debug/net10.0/ScriptAssist.Tests.dll
```

Avalonia editor tests (`SynthMultiViewer.Tests/EditorCompletionTests`) need `-parallelMode none`. Headless dispatcher is not thread-safe. Prefer `LanguageService.Analyze` in `ScriptAssist.Tests` for language rules.

Prefer real installed plugins and sibling GitHub AVSI/PY (xClean, FrameRateConverter, Shader, havsfunc) over throwaway dummy files. When a test needs a graph, an `IncludeReader` lambda is enough.

Internals: `using` the root namespace; internals visible to the test assembly.

When fixing hover/completion, assert both the **body** and **after the function** (leak), and header silence vs `vs` in annotations.

## App wiring (this repo only)

Not part of the library, but the reference host:

- `ScriptAssistService` subclasses `ScriptLanguageFactory`, passing `ScriptCatalogs` and `ScriptIncludeIO`, and sets `IsEnabled` from `EnhanceEditorWithAutoComplete`. It does not assemble `LanguageProfile`s.
- `AppSettingsData.EnhanceEditorWithAutoComplete` (default true) is the one switch. `FrameworkDetectionService.Apply` writes `factory.IsEnabled` and `Configure`s each language with a NUL-joined path/plugin-folders/replace-plugins key. The factory must not call `VsHelper.SetDllPath`.
- Prefetch catalogs when the setting is on; Ctrl+Shift+R forces `Refresh`. `BindableTextEditor` maps `ScriptKind.ToString()` to the factory ids.

## Known holes (do not “fix” by expanding into a Python IDE)

- Multi-key plugin returns stay unknown.
- `std.Split`-style array-of-nodes is not a distinct type (`FromReturn` strips `[]`).
- GScript `if` / `Eval` are not AviSynth scopes.
- Imported Python `class` members are not parsed.
- `import os` / stdlib: silent by design.
- Fraction members: `clip.fps.` stays silent.
- Nested FrameEval `def` inside a call is not a separate type environment.
