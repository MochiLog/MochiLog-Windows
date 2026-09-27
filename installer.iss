#define AppName "MochiLog Windows"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#define AppPublisher "MochiLog"
#define AppExe "MochiLog.Windows.exe"

[Setup]
AppId={{9F56A495-2D39-440B-A51B-DC9E26763C35}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/MochiLog/MochiLog-Windows
AppSupportURL=https://github.com/MochiLog/MochiLog-Windows/issues
AppUpdatesURL=https://github.com/MochiLog/MochiLog-Windows/releases
DefaultDirName={localappdata}\Programs\MochiLog Windows
DefaultGroupName=MochiLog Windows
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=Build
OutputBaseFilename=MochiLog-Windows-Alpha-Setup
SetupIconFile=src\MochiLog.Windows\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
UninstallFilesDir={app}\Uninstall
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
LicenseFile=LICENSE
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "Build\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Build\Collector\pymobiledevice3.exe"; DestDir: "{app}\Collector"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}\Licenses"; DestName: "MochiLog-GPL-3.0.txt"; Flags: ignoreversion
Source: "Build\Licenses\*"; DestDir: "{app}\Licenses"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MochiLog Windows"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall MochiLog Windows"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,MochiLog Windows}"; Flags: nowait postinstall skipifsilent
