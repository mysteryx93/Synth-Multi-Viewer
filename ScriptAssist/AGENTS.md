# ScriptAssist — implementation notes

Consumer setup and API examples: [README.md](README.md). These notes describe implementation constraints and regression expectations; they do not guarantee complete language support.

## Boundaries

- Keep one assembly. Never reference `ApiVapourSynth`, `ApiAviSynth`, or the app; never load a native core or execute scripts.
- Keep language behavior here, including type inference and `clip.` completion. The host supplies catalogs and include readers; it does not assemble language rules.
- Keep the consumer API centered on `ScriptLanguageFactory`, `GetAsync`, and `EditorAssist`. The factory owns the built-in language ids; the profile-list constructor is for tests.
- `LanguageService` and `CatalogCache` remain language-agnostic. Ranking belongs to `ILanguage.CompletionPriority`; do not special-case clip types in the engine.
- Unless explicitly requested, exclude full parsers/type checkers, eval/GScript scopes, stdlib/numpy/`__all__`, array/list element types, map keys, imported Python classes, nested FrameEval function environments, and `f.props`. Small static-analysis improvements remain appropriate.

## Structure

| Location | Namespace suffix after `HanumanInstitute.ScriptAssist` | Role |
| --- | --- | --- |
| Project root | none | Public services, factory, catalog and file helpers |
| `Models/` | none | Public contracts; preserve namespace when moving files |
| `Utilities/` | none | Internal scanning and parsing helpers |
| `AviSynth/` | `.AviSynth` | Public language/parsers; internal binding and type walking |
| `VapourSynth/` | `.VapourSynth` | Public language/parsers; internal binding and type walking |
| `AvaloniaEdit/` | `.AvaloniaEdit` | Editor integration and popup presenters |

- Do not add an `Engine/` folder or share language global usings. Import language namespaces at composition points only.
- Static scanners, splitters, type tables, regex patterns, parsers, and binders are intentional. Do not turn them into services or `Symbol` extension methods.
- Prefer `IReadOnlyList` for stored lists and `IEnumerable` for streamed paths. Existing `Symbol.Parameters` is an array.
- Keep `LanguageService.Analyze` internal. Tests have `InternalsVisibleTo`; consumers use `GetAsync`.
- Use the existing Validators dependency. `HasValue()` means non-null/non-empty, not non-whitespace; preserve whitespace checks and ordinary `Length == 0` checks.

## Analysis and source handling

`GetAsync` awaits the catalog, then analyzes off-thread. A snapshot masks the document, binds names/scopes/imports, and merges buffer symbols over native symbols by name. Requests overlay scopes at the caret, read the expression, and produce completion, insight, and hover. Inside comments/strings, assistance is suppressed; function headers suppress call insight.

The snapshot key is **text + document path + catalog reference**, not editor version. Binding performs additional scans; do not assume total analysis is linear. There is no incremental parser or file-size cap. Honor cancellation in potentially long scans.

- Preserve UTF-16 offsets when masking comments/strings or joining lines; retain newlines when masking.
- `TypeRef.Root` means an empty completion path, never a stored value type. Empty assignment expressions and tuples must not become Root.
- Unwrap matching outer parentheses before call/ternary inference. `(core.std.BlankClip())` should retain its node type.
- AviSynth preprocessing order is **join continuations, then mask** (`AviSynthPatterns.Clean`). A leading `\` can join a statement even inside a later triple-quoted string. Keep replacements the same length.
- AviSynth recognizes hash comments, `/* */`, `/[ ]/`, `[* *]`, triple quotes, and doubled quotes. Preserve the correct block closer for each opener.
- VapourSynth recognizes hash comments, single/double/triple quotes, and backslash escapes. Literal inference needs original RHS text, with comments removed and offsets aligned.

## Completion and hover

- Locals/properties hover as a type only. Suppress text identical to the identifier.
- A named argument `name=` belongs to its call, even when unresolved; never fall through to a same-named local.
- Function parameter names are silent in headers. Preserve header suppression at an unclosed EOF header (`HeaderEnd == End`). In `clip: vs.VideoNode`, `vs` may hover but `VideoNode` must not.
- Completion hints show function parameters or the local/property type. Preserve wrapping, line limits, and truncation in `CompletionData`.
- VS display names come from `VapourSynthTypes.Display` / `DisplayReturn`: use `VideoNode`, not `vnode`, for local hints. Format constants are integer properties; `VideoNode`/`AudioNode` on `vs` remain namespaces.
- `[` may commit a completion but must not trigger member completion. Insight within brackets belongs to the enclosing call.

## Language rules

### AviSynth

- Compare identifiers ordinal-ignore-case. `last` is always a clip. Unknown called names default to clip except entries in `AviSynthInternals`; clip property syntax also uses that return table.
- Preserve explicit and implicit-first-clip call handling, including bound calls and implicit-last overloads.
- Bind function parameters/assignments in `BindingScope`; keep `last`, top-level assignments, and `global x =` in script names. Imported AVSI headers contribute signatures, never their parameter locals.
- A header without `{` ends before the next function, or at EOF if none follows. Retain incomplete parameter-list spans.
- Preserve `Default(x, y)` inference, clip preference in ternaries, and current last-assignment-wins behavior. GScript/Eval are not scopes.
- Header type words such as `string "Preset"` are not function calls; `String(5)` still is. Mask comments before looking for ternary delimiters.

### VapourSynth

- Compare identifiers ordinally. Native catalogs alone supply `core.ns.Func`; do not expose those functions at statement root.
- Offer members only for a known receiver. `clip = core.std.BlankClip()` enables node properties and bound plugin namespaces; unknown receivers stay silent.
- Dotted assignments must not create locals. Preserve node types through supported `+`, `*`, and slice expressions.
- `FromReturn` ignores empty semicolon parts; two or more return keys stay unknown. Use realistic `clip:vnode;` return strings in tests. Node arrays currently collapse to the node type.
- Function scopes follow logical statements and the statement’s initial indentation, including indented methods/nested defs; imported-module exports use column-zero defs only. Blank/comment/masked-string lines must not end a body. Outer scopes overlay before inner scopes. Name lookup, aliases, and function symbols share that overlay; a later assignment shadows a function of the same name.
- Infer parameters from supported annotations/defaults, then the single naming convention `clip` → VideoNode. Do not guess `Input`, `src`, or `self`. Annotations map the last identifier through the type table (`VideoNode`, `AudioNode`, `VideoFrame`, `VideoFormat`/`Format`, `Core`, primitives). Quoted forms, `Optional[T]`, and `T | None` unwrap; mixed unions stay unknown. Aliases such as `from vapoursynth import VideoNode as Node` resolve through bound names.
- Imported `.py` definitions enter the importing document through `import`/`from`; do not expose every discovered file at root. Failed imports must not overwrite names with Unknown. A `script:id` member is a module, not a function call.
- Declare `Formats`/`Families` before `ModuleMembers` in `VapourSynthHostTypes` to avoid static initialization failures. Fraction members (`clip.fps.`) remain unsupported.

## Catalogs, includes, and editor lifecycle

- Catalog callbacks run in the background. `SetKey` only remembers configuration; `Refresh` starts enumeration; cancelling `GetAsync` cancels the wait, not native work. Enumeration failures become an empty catalog. The cache retains the current task until configuration changes or refresh is forced.
- `IsEnabled` is a factory bool; the editor options callback follows it. Disabled `Configure` stores the key, disabled `Refresh` does nothing, and `Create` returns null. Retained services skip `GetAsync` catalog work while the factory is disabled. Duplicate profile ids throw.
- `IncludeReader(specifier, fromPath)` returns full resolved path + text, or null. The host owns I/O; `ScriptFiles` builds candidates and invokes its supplied reader. Missing/unreadable candidates must return null so lookup can continue.
- Use absolute include paths directly; otherwise search beside the importing file, then host roots. Python leading dots are relative to that file's directory, never a rooted path produced by replacing dots. Track resolved paths to prevent cycles.
- Pass `documentPath` through `GetAsync`/`Bind`. Unsaved buffers lack a sibling directory but can use configured roots. Python site-packages belongs in script search roots, never native autoload directories.
- `EditorAssist` owns attach/detach, cancellation, debounce, and stale-result rejection; presenters own popups. Keep parser internals out of editor options; exposing `Reply` is sufficient. No Splat registration helper.

Reference host: `ScriptAssistService` supplies `ScriptCatalogs` and `ScriptIncludeIO`. `FrameworkDetectionService` configures keys after native setup; the factory must not call `VsHelper.SetDllPath`. `EnhanceEditorWithAutoComplete` is the enablement setting; `BindableTextEditor` maps `ScriptKind` to factory ids and passes the open file path. Autoload AVSI/AVS parsing and catalog mapping stay at this host boundary.

## Analysis notes

- VS bindings scan logical statements (brackets, `;`, `\` continuations) so assignments keep original RHS spans. Column-0 `def` symbols, return annotations, and annotation-only names use the existing type table. Imports follow source order and function scope; module identity is the resolved path.
- Parameter names are language-specific (`OfPython` / `OfAviSynth`). Bound node completion filters video vs audio first arguments. `UnionByName` keeps native overload groups. Argument completion reuses the resolved call frame and skips `*`/`/` separators.
- `ILanguageService.Invalidate` increments a generation and drops snapshots independently of catalog identity; in-flight analysis must not publish a stale snapshot. Factory `Refresh` invalidates every profile. `Create` returns null while disabled.
- Expression reading walks consecutive `()` / `[]` suffixes. AviSynth `BackslashLineContinuations` joins before mask; Python joins `\` continuations outside strings and comments. Unterminated single-line strings recover at the next newline. CRLF is one newline.

## Validation

Run from the repository root; these are xunit.v3 executables on .NET 10, not VSTest projects:

```bash
dotnet build ScriptAssist.Tests/ScriptAssist.Tests.csproj
dotnet ScriptAssist.Tests/bin/Debug/net10.0/ScriptAssist.Tests.dll
```

- Prefer `LanguageService.Analyze` tests for language rules. Run Avalonia editor tests in `SynthMultiViewer.Tests/EditorCompletionTests` with `-parallelMode none`; the headless dispatcher is not thread-safe.
- Test inside and after function scopes, header silence, and `vs` annotation hover. Include comments, multiline/incomplete input, and realistic native signatures.
- Prefer representative installed/sibling scripts: xClean trailing `\`, FrameRateConverter leading `\` and `[** *]`/triple quotes, Shader, and havsfunc. Small `IncludeReader` lambdas suffice for import graphs.
