# Synth Multi-Viewer — laws

Language-engine invariants: [ScriptAssist/AGENTS.md](ScriptAssist/AGENTS.md). Functions Explorer: [ScriptAssist/FunctionsExplorerPlan.md](ScriptAssist/FunctionsExplorerPlan.md).

## Types

A class does **one job**. Unless that job changes, the class does not. Native dump, `Symbol` mapping, autoload-script merge, snapshot bind, disk read, and a window are separate types.

**One type per file.** The file name is the type name. Do not put `AviSynthIncludeSource` and `VapourSynthIncludeSource` in one `ScriptIncludeIO.cs`. Nested types stay with their owner.

Do not grow a static host helper that calls the native core, maps DTOs, walks folders, and parses scripts (`ScriptCatalogs` was that). Do not explode binders and scanners into injectable services.

## Collaborators

Process and disk boundaries are **named roles**, not `File.ReadAllText` wrappers. Host implements `IVapourSynthNativeCatalog`, `IAviSynthNativeCatalog`, `IScriptDirectory`, `IIncludeSource`, and `IAssistSession`. Disk I/O goes through `IFileSystemService` (`ScriptAssist/Services/`, extractable), including every member already on `IFileSystem` / `IFile` / `IDirectory` / `IPath`. Tests swap a Fake (`MockFileSystem`) or Moq — not temp files, `SkipWhen(File.Exists)`, or other disk work-arounds. `System.IO` is only for values that are predictable in tests **and** missing from those interfaces (`IOException`, `FileMode`, `SearchOption`); write `System.IO.` so they stay visible. Do not remove `System.IO` from implicit usings. `IEnvironmentService` stays in the app (CLI, version, app-data folder); it is not an include or catalog collaborator. Native path probes in Api* stay on the real disk. The factory maps natives to `Symbol` and caches. `ISymbolSource` is only `CatalogCache`'s input after mapping.

**Do not inject `Func<>` / `delegate` as a collaborator.** That is how production gets designed so a test can pass `() => []` and never name a type.

A one-method interface is correct only when that method **is** the role (dump the core, resolve an import). It is wrong when it is a lambda with a type name (`ITextFile.TryRead`, `IAssistGate.IsEnabled`) or when two interfaces split one disk (`ITextFile` + directory listing). Enablement is `IScriptLanguageFactory.IsEnabled`. `Create` returns null; the editor skips. `LanguageService` analyzes.

Do not add a second constructor, a settable internal callback, or `InternalsVisibleTo` hooks so tests stay easy. If the host must supply a catalog, that type exists for the host.

## Tests are not a design input

Do not alter production so tests compile. Tests mock the interfaces the host already needed. Moq belongs at those ports.

Language facts (hover, completion, parse) take **data**: script text and `Symbol` lists. Do not Moq `ILanguage` to prove `Crop` hover. Do not add `EmptySymbols` or `AlwaysOnGate` to production for the test project.

A local `() =>` in a test is fine (throw act, Moq `Returns`). A public `Func<IReadOnlyList<Symbol>>` on the factory is not.

## What not to generate

These show up by default and are wrong here:

- Catalog / include / path / enablement as `Func` or `delegate` parameters
- A one-bool collaborator (`IAssistGate`, `AllowRequests`) so analysis can poll factory enablement
- `ITextFile` / other one-method `File` wrappers; disk I/O is `IFileSystemService`
- `File.` / `Directory.` / `Path.` when `IFileSystemService` already has the member; tests that hit the real disk (`TemporaryScript`, `Path.GetTempPath`, `SkipWhen`)
- Stripping `System.IO` from implicit usings in `Directory.Build.props` to find leftovers
- Factory taking pre-mapped `ISymbolSource` so tests can skip `VapourSynthSymbolSource`
- `ScriptLanguageFactory(IReadOnlyList<LanguageProfile>)` or any ctor documented “for tests”
- Two types in one file to “keep IO together”
- Host mapping of native plugins in the app when only assist consumes the list
- A second native walk or header parse in a window or view-model
- Optional parameters whose only caller is a test
- UI tests for markup, labels, geometry, or command wiring by control name
- Turning `VapourSynthBinder` / static parsers into DI services

## Split

| Place | Owns |
|---|---|
| ApiAviSynth / ApiVapourSynth | Throwaway core, copy metadata out |
| ScriptAssist | `Symbol`, grouping, bind, browse, hover, mapping from dump DTOs |
| App | Engine paths, dump adapters, plugin-folder disk via `IFileSystemService`, window, insert |

ScriptAssist never loads a native core. The app never groups plugins or decides what Autoload means.

## Tests — what earns a place

Keep a test when it locks **observable behavior** that would otherwise regress.

One lock per fact. A hover/`Analyze` assertion already covers the string; do not also test the formatter that produced it. A presenter test for wrap or size must not re-assert that same string.

Updating a test that would fail is required. Adding another because this task touched the type is not.

A test that only existed to finish the current task does not belong in the suite.

## UI tests are last resort

Default: **do not write a UI test.** Do not open a window. Do not walk the visual tree.

You do not need a test to align a button, set a label, move a control, restyle chrome, or prove a page looks right. Look at the view. If the change is markup, there is no test.

Never assert display text, geometry, chrome, or command wiring by control name. Those live in the view.

A headless UI test is allowed only when a **user gesture** produces a **state change** that cannot be observed from the view-model, helper, or converter.

`TranslatePoint` and `Bounds` may aim a click. They must not be the expected value. Do not add a test whose act is “look at the control.”

If a test would fail when someone rewrites the markup without changing behavior, delete it.

When a layout bug is real, assert the **user-visible outcome**. Do not pin the style that was used to fix it.

Do not add a second UI test for a command or key already covered from the view-model or another gesture.

## Shape

Name: `Method_Action_Result` — three segments, single underscores.

```csharp
[Fact]
public void Parse_ValidInput_ReturnsValue()
{
    const string text = "example";

    var result = Parse(text);

    Assert.Equal("example", result);
}
```

The body is three sections separated by a **single blank line** — exactly two blank lines in the method, not comments: Prepare, Act, Assert.

- Prepare builds inputs. No blank lines inside a section. Theory `[InlineData]` is not a substitute for a prepare block when the method still has setup. When the method has no setup beyond parameters, Act/Assert (one blank line) is enough.
- Act is one operation (or one user gesture). A throw lambda is `var act = () =>`.
- Assert is only assertions.
- Split mixed scenarios into separate methods instead of several act/assert cycles. Sequential setup (cache, invalidate, graph) stays in Prepare; the mutating call is Act.
- `try`/`finally` and `Assert.SkipWhen` wrap the three sections; they are not extra sections.
- A callback used in Prepare is a local function at the end of Prepare (C# allows use-before-declaration). Do not assign a lambda to a **production** delegate. Test Moq `Setup`/`Returns` may use lambdas.
- Prefer `const` for unchanging locals (`string`/`int`/`bool` literals that are never reassigned).
- Local functions belong at the end of the method, except Prepare callbacks, which stay at the end of Prepare so later setup can use them.
- Do not write `// Arrange`, `// Act`, `// Assert`.

A dispatcher flush belongs with the act that needs a layout pass, not as its own section.

## Headless

Use a UI-thread fact when the test needs a window, asset loader, or framework objects that require the dispatcher. That is not permission to inspect markup.

Do not mix UI-thread facts and a headless session `Dispatch` in the same test project. That combination hangs after discovery.

Tests that never touch the UI framework use `[Fact]` / `[Theory]` and must not touch the dispatcher.

Build the app project before UI tests. Skipping project dependencies leaves stale markup in the test output. UI tests need `-parallel none` when the dispatcher is not thread-safe.

## Run

These are xunit.v3 executables. `dotnet test` / VSTest is unsupported.

```bash
dotnet build Path/To.Tests.csproj
dotnet Path/To.Tests/bin/Debug/<tfm>/To.Tests.dll
```

`-class` needs the fully qualified type name. Combining `-class` and `-method` is AND.
