# Synth Multi-Viewer

**Cross-platform editor and viewer for VapourSynth and AviSynth**

### Why?

I was growing tired of the lack of tooling support for VapourSynth, and especially AviSynth, on Linux.

Comparing script variants is also very time-consuming and difficult without proper tools.

### Support

VapourSynth and AviSynth

x64, Windows x86, and ARM64

Windows, Linux and MacOS

### Features

- Auto-detect VapourSynth and AviSynth library and plugin locations, or customize library and plugin locations
- Edit multiple scripts with tabs
- Run multiple previews at once
- Zoom and pan to look at details
- Frame navigation, zoom and pan are shared between preview tabs
- Rename each tab for reference
- Full-screen preview
- Copy frame to clipboard
- Video properties window (clip format before display conversion, plus frame properties)
- Code highlight for VapourSynth and AviSynth

### MacOS Installation

Download the ZIP that matches the Mac: `MacOS_arm64` on Apple Silicon, `MacOS_x64` on Intel.

Extract it and drag `SynthMultiViewer.app` into `/Applications`.

The first launch is blocked by Gatekeeper because the build is not notarized. Clear the quarantine flag, including files inside the bundle:

    xattr -dr com.apple.quarantine /Applications/SynthMultiViewer.app

Then open the app from Applications. If macOS still refuses, Control-click the app and choose Open.

VapourSynth and AviSynth must match that architecture. On Apple Silicon, Homebrew installs are ARM64, so use the ARM64 app.

### VapourSynth / AviSynth API for .NET

This repo also contains .NET API wrapper for [VapourSynth](ApiVapourSynth/) and [AviSynth](ApiAviSynth/).

### ScriptAssist

[ScriptAssist](ScriptAssist/README.md) provides completion, call insight, and hover for VapourSynth and AviSynth, with AvaloniaEdit and headless APIs. It analyzes text and supplied catalogs without executing scripts or requiring Python.

This is a very powerful editor. AI was **definitely** used to write this module.

### License

[MIT License](LICENSE.md)

### Author

[Etienne Charland](https://www.hanumaninstitute.com) — Soul Architect | Reality Engineering | Coherence programming

### TODO

- Encode
- Pipette — YUV/RGB under the cursor
- Masks
- Plane view — Y / U / V (or RGB) as a display mode
- vS Output view