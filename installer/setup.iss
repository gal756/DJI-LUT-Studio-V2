; =====================================================================
; DJI LUT Studio - Inno Setup Compiler Script
; Standalone 64-bit Windows Installer with Multi-GPU Acceleration
; =====================================================================

#define MyAppName "DJI LUT Studio"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "DJI LUT Studio Open Source Community"
#define MyAppURL "https://github.com/gal756/LUT-adder"
#define MyAppExeName "DJI_LUT_Studio.exe"
#define SourceDir "C:\Users\gal75\AppData\Local\Temp\DJI_LUT_Studio_Dist"

[Setup]
AppId={{E65F1784-A823-45F4-A018-8A7E97FF4312}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion} (Multi-GPU Edition)
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile={#SourceDir}\LICENSE
InfoBeforeFile={#SourceDir}\THIRD_PARTY_LICENSES.md
OutputDir=C:\Users\gal75\Desktop
OutputBaseFilename=DJI_LUT_Studio_Setup_v2.0.0
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile={#SourceDir}\app.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
