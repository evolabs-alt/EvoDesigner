[Setup]
AppName=TaggJack
AppVersion=1.1.0
AppPublisher=TaggJack
AppPublisherURL=https://github.com/ehtearena/TaggJack
AppSupportURL=https://github.com/ehtearena/TaggJack/issues
AppUpdatesURL=https://github.com/ehtearena/TaggJack/releases
DefaultDirName={autopf}\TaggJack
DefaultGroupName=TaggJack
AllowNoIcons=yes
LicenseFile=
OutputDir=installer
OutputBaseFilename=TaggJack-Setup-v1.1.0
SetupIconFile=
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\TaggJack"; Filename: "{app}\TaggJack.exe"
Name: "{group}\{cm:UninstallProgram,TaggJack}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\TaggJack"; Filename: "{app}\TaggJack.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\TaggJack"; Filename: "{app}\TaggJack.exe"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\TaggJack.exe"; Description: "{cm:LaunchProgram,TaggJack}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCR; Subkey: ".evotpl"; ValueType: string; ValueName: ""; ValueData: "TaggJack.Template"; Flags: uninsdeletevalue
Root: HKCR; Subkey: "TaggJack.Template"; ValueType: string; ValueName: ""; ValueData: "TaggJack Template"; Flags: uninsdeletekey
Root: HKCR; Subkey: "TaggJack.Template\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\TaggJack.exe,0"
Root: HKCR; Subkey: "TaggJack.Template\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\TaggJack.exe"" ""%1"""