#define AppName "__APP_NAME__"
#define AppInternal "__APP_INTERNAL__"
#define AppVersion "__APP_VERSION__"
#define PublishDir "__PUBLISH_DIR__"
#define OutputDir "__OUTPUT_DIR__"
#define OutputFile "__OUTPUT_FILE__"
#define IconFile "__ICON_FILE__"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
UninstallDisplayName={#AppName}
AppPublisher=Hanuman Institute
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppInternal}.exe
DefaultDirName={autopf}\Hanuman Institute\{#AppInternal}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputFile}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ChangesAssociations=yes

[Tasks]
Name: associatefiles; Description: "Associate .vpy, .avs, and .avsi files with {#AppName}"; GroupDescription: "Additional tasks:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppInternal}.exe"

[Registry]
Root: HKA; Subkey: "Software\Classes\{#AppInternal}.Script"; ValueType: string; ValueName: ""; ValueData: "VapourSynth/AviSynth Script"; Flags: uninsdeletekey; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\{#AppInternal}.Script\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppInternal}.exe,0"; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\{#AppInternal}.Script\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppInternal}.exe"" ""%1"""; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\.vpy\OpenWithProgids"; ValueType: string; ValueName: "{#AppInternal}.Script"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\.avs\OpenWithProgids"; ValueType: string; ValueName: "{#AppInternal}.Script"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\.avsi\OpenWithProgids"; ValueType: string; ValueName: "{#AppInternal}.Script"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\Applications\{#AppInternal}.exe"; Flags: uninsdeletekey; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\Applications\{#AppInternal}.exe\SupportedTypes"; ValueType: string; ValueName: ".vpy"; ValueData: ""; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\Applications\{#AppInternal}.exe\SupportedTypes"; ValueType: string; ValueName: ".avs"; ValueData: ""; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\Applications\{#AppInternal}.exe\SupportedTypes"; ValueType: string; ValueName: ".avsi"; ValueData: ""; Tasks: associatefiles

[Run]
Filename: "{app}\{#AppInternal}.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
