; Installer for Cache Cleaner for RedM & FiveM (Inno Setup 6)
; To build: build the project in "Release", then compile this file with Inno Setup.
; Output: installer\Output\CC-Setup-<Version>-x64.exe

#define AppName "Cache Cleaner for RedM & FiveM"
#define ShortName "Cache Cleaner"
; The version can be overridden when compiling: ISCC /DAppVersion=1.2.3 setup.iss
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "ProfessorDevlin"
#define AppExe "Cache Cleaner.exe"
#define BuildDir "..\bin\Release\net48"

[Setup]
AppId={{B6F2D9C4-3E71-4A8D-9C5B-71E0A4F2D813}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=© 2026 {#AppPublisher}
; Installed per user without admin rights (like Discord or VS Code): %LocalAppData%\Programs\Cache Cleaner
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#ShortName}
DefaultGroupName={#ShortName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=CC-Setup-{#AppVersion}-x64
CloseApplications=yes
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#BuildDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#ShortName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#ShortName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#ShortName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Quick Clean shortcuts created by the tool would point to the removed program
Type: files; Name: "{userdesktop}\Quick Clean – *.lnk"
