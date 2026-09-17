; Inno Setup 6 — Screen Audio Router
; Custom install path is the default UX (Select Destination Location page).

#define AppName "Screen Audio Router"
#define AppVersion "0.1.0"
#define AppPublisher "ScreenAudioRouter"
#define AppExeName "ScreenAudioRouter.exe"
#define PublishDir "..\src\ScreenAudioRouter\bin\publish\win-x64"

[Setup]
AppId={{8F3C2A10-9B4E-4E1A-9C7D-ScreenAudio001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\ScreenAudioRouter
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Allow user to pick any folder
DisableDirPage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=.\Output
OutputBaseFilename=ScreenAudioRouter-Setup-{#AppVersion}
SetupIconFile=..\src\ScreenAudioRouter\Assets\ScreenAudioRouter.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
ShowLanguageDialog=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "开机启动（登录后自动运行）"; GroupDescription: "其他选项:"; Flags: unchecked

[Files]
; Self-contained publish folder must exist — run build\publish.ps1 first
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "立即启动 {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
