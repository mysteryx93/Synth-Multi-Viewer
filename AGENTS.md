# Tests

Follow this when adding or changing tests. Language-engine invariants for ScriptAssist: [ScriptAssist/AGENTS.md](ScriptAssist/AGENTS.md).

## What earns a place

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
- A callback used in Prepare is a local function at the end of Prepare (C# allows use-before-declaration). Do not assign a lambda to a delegate local.
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
