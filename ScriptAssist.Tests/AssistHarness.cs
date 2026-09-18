using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;

namespace HanumanInstitute.ScriptAssist.Tests;

internal static class AssistHarness
{
    internal static readonly IReadOnlyList<Symbol> Vs =
    [
        new("core.std.Crop", ["clip:vnode", "left:int:opt", "right:int:opt"], ReturnType: "clip:vnode;"),
        new("core.std.BlankClip", ["width:int:opt", "height:int:opt"], ReturnType: "clip:vnode;"),
        new("core.rife.RIFE", ["clip:vnode", "model:int:opt"], ReturnType: "clip:vnode;"),
        new("core.std.SelectEvery", ["clip:vnode", "cycle:int", "offsets:int[]"], ReturnType: "clip:vnode;"),
        new("core.svp1.Super", ["clip:vnode"], ReturnType: "clip:vnode;clip:vnode;"),
        new("core.std.AudioTrim", ["clip:anode", "first:int:opt"], ReturnType: "clip:anode;")
    ];

    internal static LanguageService VsService(IncludeReader? read = null) =>
        new(new VapourSynthLanguage(read), new CatalogCache(() => []));

    internal static LanguageService AvsService(IncludeReader? read = null) =>
        new(new AviSynthLanguage(read), new CatalogCache(() => []));

    internal static IncludeReader HavsReader(string? text) =>
        FilesReader(text == null ? [] : new Dictionary<string, string> { ["havsfunc"] = text });

    internal static IncludeReader FilesReader(IReadOnlyDictionary<string, string> files) =>
        (specifier, _) => files.TryGetValue(specifier, out var text)
            ? new IncludeFile("/plugins/" + specifier.TrimStart('.') + ".py", text)
            : null;
}
