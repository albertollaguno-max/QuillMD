#define MyAppName "QuillMD"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "QuillMD"
#define MyAppURL "https://github.com/albertollaguno-max/QuillMD"
#define MyAppExeName "QuillMD.exe"

[Setup]
AppId={{B7E4F2A1-3C5D-4E6F-8A9B-1C2D3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist
OutputBaseFilename=QuillMD-Setup-{#MyAppVersion}
SetupIconFile=..\Resources\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associatemd"; Description: "Asociar archivos .md con {#MyAppName}"; GroupDescription: "Asociaciones de archivo:"

[Files]
Source: "..\bin\Release\net9.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; File association for .md files
Root: HKCU; Subkey: "Software\Classes\.md"; ValueType: string; ValueName: ""; ValueData: "QuillMD.MarkdownFile"; Flags: uninsdeletevalue; Tasks: associatemd
Root: HKCU; Subkey: "Software\Classes\QuillMD.MarkdownFile"; ValueType: string; ValueName: ""; ValueData: "Archivo Markdown"; Flags: uninsdeletekey; Tasks: associatemd
Root: HKCU; Subkey: "Software\Classes\QuillMD.MarkdownFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associatemd
Root: HKCU; Subkey: "Software\Classes\QuillMD.MarkdownFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associatemd

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Ejecutar {#MyAppName}"; Flags: nowait postinstall skipifsilent
