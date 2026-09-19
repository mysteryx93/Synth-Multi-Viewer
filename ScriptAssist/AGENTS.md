# ScriptAssist — implementation notes

Consumer setup and API examples: [README.md](README.md). These notes describe implementation constraints and regression expectations; they do not guarantee complete language support.

## Boundaries

- Keep one assembly. Never reference `ApiVapourSynth`, `ApiAviSynth`, or the app; never load a native core or execute scripts.
- Keep language behavior here, including type inference and `clip.` completion. The host supplies catalogs and include readers; it does not assemble language rules.
- Keep the consumer API centered on `ScriptLanguageFactory`, `GetAsync`, and `EditorAssist`. The factory owns the built-in language ids; the profile-list constructor is for tests.
- `LanguageService` and `CatalogCache` remain language-agnostic. Ranking belongs to `ILanguage.CompletionPriority`; do not special-case clip types in the engine. Member lists are alphabetical; named-argument completions keep a higher priority so they stay above the rest of the list. Do not demote plugin namespaces on `clip.`.
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

`GetAsync` awaits the catalog, then analyzes off-thread, including cache hits. A snapshot masks the document and binds names, scopes, and imports. Languages overlay buffer symbols over native catalog entries when resolving members, calls, and hover. Requests overlay scopes at the caret, read the expression, and produce completion, insight, and hover. Inside comments/strings, assistance is suppressed; function headers suppress call insight.

The snapshot key is **text + document path + catalog reference**, not editor version. `LanguageService` retains a small LRU of snapshots (multiple documents, evicting older revisions of the same path) with a retained-byte cap that includes imported modules, scopes, and parameter strings. Concurrent requests for the same key share one in-flight bind; every caller awaits that task through its own token, and a build with no remaining waiters is cancelled. Invalidation drops cached snapshots and the in-flight lookup so the next request cannot observe a stale bind. Binding performs additional scans; do not assume total analysis is linear. There is no incremental parser. Honor cancellation in potentially long scans.

Include cache lifecycle is documented in README; parsed exports and failed path lookups are LRU-bounded, unpinned entries also have a byte cap, the current document's working set is pinned, and `Invalidate` still clears everything. Import expansion is depth-limited. Do not assume includes are rebound on every document edit. Snapshot invalidation clears the language include cache in the same generation transition. Cached snapshot sizes follow live binding views.

- Preserve UTF-16 offsets when masking comments/strings or joining lines; retain newlines when masking.
- `TypeRef.Root` means an empty completion path, never a stored value type. Empty assignment expressions and tuples must not become Root.
- Unwrap matching outer parentheses before call/ternary inference. `(core.std.BlankClip())` should retain its node type.
- AviSynth preprocessing order is **join continuations, then mask**. `BufferLexer.Mask` joins when `BackslashLineContinuations` is set; `AviSynthPatterns.Clean` delegates to that and must not join again. A leading `\` can join a statement even inside a later triple-quoted string. Keep replacements the same length.
- AviSynth recognizes hash comments, `/* */`, `/[ ]/`, `[* *]`, triple quotes, and doubled quotes. Preserve the correct block closer for each opener.
- VapourSynth recognizes hash comments, single/double/triple quotes, and backslash escapes. Literal inference needs original RHS text, with comments removed and offsets aligned.

## Completion and hover

- Locals/properties hover as a type only. Suppress text identical to the identifier.
- A named argument `name=` belongs to its call, even when unresolved; never fall through to a same-named local.
- Function parameter names are silent in headers. Preserve header suppression at an unclosed EOF header (`HeaderEnd == End`). In `clip: vs.VideoNode`, `vs` may hover but `VideoNode` must not.
- Completion hints and hover share `Symbol.Tip`: function signature, or the type only for locals, properties, and plugin namespaces (`plugin` on `core.std` and `clip.std`). Named-argument hover is the parameter type; those completions insert `name=` and have no side hint. Function `Signature` uses `DisplayName` (last dotted segment), so `core.std.BlankClip` shows `BlankClip(...)`. Catalog `Name` stays fully qualified. Wrap and truncation come from `EditorAssistOptions.Hint` and `Hover` (`AssistTipSize`); do not hard-code widths in presenters. Short tips size to content (MaxWidth, not Width). Hover defaults are wider than the completion side panel and also wrap call-insight headers (`OverloadProvider.CurrentHeader` is a wrapping block, not a one-line string). Fluent's tooltip chrome is 320px (`ToolTipContentMaxWidth`); hover must set MaxWidth on a `ToolTip` instance so that cap does not wrap signatures.
- VS display names come from `VapourSynthTypes.Display` / `DisplayReturn`: use `VideoNode`, not `vnode`, for local hints. Keep `[]` on parameter types (`color:float[]` is not `float`). A parameter named `format` with native type `int` displays as `VideoFormat` (BlankClip/`resize` format ids, and `clip.format`). Format constants (`vs.YUV420P8`) stay int. `VideoNode`/`AudioNode` on `vs` remain namespaces.
- `[` may commit a completion but must not trigger member completion. Insight within brackets belongs to the enclosing call.

## Language rules

### AviSynth

- Compare identifiers ordinal-ignore-case. `last` is always a clip. Unknown called names default to clip except entries in `AviSynthInternals`; clip property syntax also uses that return table.
- Preserve explicit and implicit-first-clip call handling on one catalog signature. Bound `last.Crop` / `clip.Crop` skip the first clip; root `Crop(10,` and `BlankClip(length=100,` do the same when the first argument is not a clip (numbers, strings, and `name=` are not clips). Do not present implicit last as a second overload. Native `$InternalFunctions$` lists the same filter many times with one Param$ string; insight keeps one copy of each distinct signature.
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
- `ILanguageService.Invalidate` increments a generation and drops snapshots independently of catalog identity; in-flight analysis must not publish a stale snapshot. Factory `Refresh` invalidates every profile. `Configure` with a new catalog key also invalidates include state, including keys stored while disabled. `Create` returns null while disabled.
- Expression reading walks consecutive `()` / `[]` suffixes. AviSynth `BackslashLineContinuations` joins before mask; Python joins `\` continuations outside strings and comments. Unterminated single-line strings recover at the next newline. CRLF is one newline.

## Validation

Run from the repository root; these are xunit.v3 executables on .NET 10, not VSTest projects:

```bash
dotnet build ScriptAssist.Tests/ScriptAssist.Tests.csproj
dotnet ScriptAssist.Tests/bin/Debug/net10.0/ScriptAssist.Tests.dll
```

- Prefer `LanguageService.Analyze` tests for language rules. Suite shape, placement, and UI-test rules: repository [AGENTS.md](../AGENTS.md). Unqualified native names lock once on hover; insight wrap locks wrap/`MaxWidth` only. Do not add `Symbol.DisplayName`/`Signature` facts for the same string. Run Avalonia editor tests in `SynthMultiViewer.Tests/EditorCompletionTests` with `-parallel none`; the headless dispatcher is not thread-safe.
- User-facing coverage is primary: completion items, call insight, and hover as `Analyze` returns them. If a name can be completed, called, and hovered, lock the surfaces that can disagree (list vs insight vs hover). Engine and parser tests do not substitute for that. Put new editor-visible behavior in `User/`; ad-hoc lexer/parser edges stay in the language files.

| Folder | Class | Role |
| --- | --- | --- |
| `User/` | `VapourSynthAssistTests` | VS complete / insight / hover contracts |
| `User/` | `AviSynthAssistTests` | AVS complete / insight / hover contracts |
| `Engine/` | `ExpressionReaderTests` | Caret expression, joins, grouping |
| `Engine/` | `IncludeCacheTests` | Include LRU, pinned working sets |
| `Engine/` | `LanguageServiceTests` | Snapshots, inflight share, invalidate, cancel |
| `AviSynth/` | `AviSynthLanguageTests` | Bind, `last`, headers, types, AVS lexer |
| `VapourSynth/` | `VapourSynthLanguageTests` | Bind, scopes, types, members, VS lexer |
| `Imports/` | `ScriptImportTests` | Import graphs, packages, cycles |
| `Imports/` | `ScriptIncludesTests` | Parameter parse, include path search |
| `Calls/` | `CallInsightTests` | Named-arg mapping, separators, recovery |
| `Host/` | `ScriptLanguageFactoryTests` | Factory, catalogs, enablement |
| `Host/` | `CompletionDataTests` | Presenters, wrap, overload UI |

- Test inside and after function scopes, header silence, and `vs` annotation hover. Include comments, multiline/incomplete input, and realistic native signatures.
- Prefer representative installed/sibling scripts: xClean trailing `\`, FrameRateConverter leading `\` and `[** *]`/triple quotes, Shader, and havsfunc. Import graphs use a `Read` local function at the end of Prepare (`IncludeFile? Read(string specifier, string? fromPath)`), not an `IncludeReader` lambda.
