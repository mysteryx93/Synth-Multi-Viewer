#define AppName "__APP_NAME__"
#define AppInternal "__APP_INTERNAL__"
#define AppVersion "__APP_VERSION__"
#define PublishDir "__PUBLISH_DIR__"
#define OutputDir "__OUTPUT_DIR__"
#define OutputFile "__OUTPUT_FILE__"
#define IconFile "__ICON_FILE__"
#define X64 "__X64__"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
UninstallDisplayName={#AppName}
AppPublisher=Hanuman Institute
#if X64 == "1"
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
#endif
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppInternal}.exe
DefaultDirName={autopf}\Hanuman Institute\{#AppInternal}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputFile}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=yes

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppInternal}.exe"

[Registry]
Root: HKLM; Subkey: "Software\Classes\{#AppInternal}.Script"; ValueType: string; ValueName: ""; ValueData: "VapourSynth/AviSynth Script"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\{#AppInternal}.Script\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppInternal}.exe,0"
Root: HKLM; Subkey: "Software\Classes\{#AppInternal}.Script\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppInternal}.exe"" ""%1"""
Root: HKLM; Subkey: "Software\Classes\.vpy\OpenWithProgids"; ValueType: string; ValueName: "{#AppInternal}.Script"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.avs\OpenWithProgids"; ValueType: string; ValueName: "{#AppInternal}.Script"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\.avsi\OpenWithProgids"; ValueType: string; ValueName: "{#AppInternal}.Script"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\Applications\{#AppInternal}.exe\SupportedTypes"; ValueType: string; ValueName: ".vpy"; ValueData: ""
Root: HKLM; Subkey: "Software\Classes\Applications\{#AppInternal}.exe\SupportedTypes"; ValueType: string; ValueName: ".avs"; ValueData: ""
Root: HKLM; Subkey: "Software\Classes\Applications\{#AppInternal}.exe\SupportedTypes"; ValueType: string; ValueName: ".avsi"; ValueData: ""

[Run]
Filename: "{app}\{#AppInternal}.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
