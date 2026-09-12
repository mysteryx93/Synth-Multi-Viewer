using System.Text.Json.Serialization;
using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Source-generated JSON metadata for settings files.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppSettingsData))]
[JsonSerializable(typeof(AppTheme))]
internal partial class AppJsonContext : JsonSerializerContext;
