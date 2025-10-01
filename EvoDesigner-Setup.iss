[Setup]
AppName=EvoDesigner
AppVersion=1.0.0
AppPublisher=EvoDesigner
AppPublisherURL=https://github.com/ehtearena/EvoDesigner
AppSupportURL=https://github.com/ehtearena/EvoDesigner/issues
AppUpdatesURL=https://github.com/ehtearena/EvoDesigner/releases
DefaultDirName={autopf}\EvoDesigner
DefaultGroupName=EvoDesigner
AllowNoIcons=yes
LicenseFile=
OutputDir=installer
OutputBaseFilename=EvoDesigner-Setup-v1.0.0
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
Name: "{group}\EvoDesigner"; Filename: "{app}\EvoDesigner.exe"
Name: "{group}\{cm:UninstallProgram,EvoDesigner}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\EvoDesigner"; Filename: "{app}\EvoDesigner.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\EvoDesigner"; Filename: "{app}\EvoDesigner.exe"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\EvoDesigner.exe"; Description: "{cm:LaunchProgram,EvoDesigner}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCR; Subkey: ".evotpl"; ValueType: string; ValueName: ""; ValueData: "EvoDesigner.Template"; Flags: uninsdeletevalue
Root: HKCR; Subkey: "EvoDesigner.Template"; ValueType: string; ValueName: ""; ValueData: "EvoDesigner Template"; Flags: uninsdeletekey
Root: HKCR; Subkey: "EvoDesigner.Template\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\EvoDesigner.exe,0"
Root: HKCR; Subkey: "EvoDesigner.Template\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\EvoDesigner.exe"" ""%1"""