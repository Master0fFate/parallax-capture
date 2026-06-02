; ───────────────────────────────────────────────────────────────────
; Parallax Capture — Inno Setup Installer
; Requires Inno Setup 7 (https://jrsoftware.org/isdl.php)
;
; Build steps:
;   1. dotnet publish -c Release -p:Platform=x64 -o publish
;   2. Open this file in Inno Setup Compiler, click Compile
;   3. Output: installer\ParallaxCapture-Setup-{#AppVersion}.exe
; ───────────────────────────────────────────────────────────────────

#define AppName        "Parallax Capture"
#define AppVersion     "1.0.5"
#define AppPublisher   "Master0fFate"
#define AppExeName     "Parallax Capture.exe"
#define AppSourceDir   "publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={userpf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=installer
OutputBaseFilename=ParallaxCapture-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile={#AppSourceDir}\..\Assets\icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
; Min Windows version: Windows 10 (10.0.10240)
MinVersion=10.0.10240

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#AppSourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppSourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#AppSourceDir}\*.xml"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM ""{#AppExeName}"""; Flags: runhidden skipifdoesntexist; RunOnceId: "KillParallax"

; ───────────────────────────────────────────────────────────────────
; .NET 10 Desktop Runtime check + install
; ───────────────────────────────────────────────────────────────────

[Code]
function IsDotNet10Installed: Boolean;
var
  ResultCode: Integer;
  TmpFile: String;
  Output: AnsiString;
  SubKeyNames: TArrayOfString;
  I: Integer;
  BaseKey: String;
begin
  Result := False;

  // Check registry (fast) — enumerate 10.0.* subkeys
  BaseKey := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  if RegGetSubkeyNames(HKLM, BaseKey, SubKeyNames) then
  begin
    for I := 0 to GetArrayLength(SubKeyNames) - 1 do
    begin
      if Pos('10.0', SubKeyNames[I]) = 1 then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;

  // Fallback: shell out to dotnet CLI
  TmpFile := ExpandConstant('{tmp}\dotnet_runtimes.txt');
  if Exec('cmd.exe', '/c dotnet --list-runtimes > "' + TmpFile + '" 2>&1', '',
          SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringFromFile(TmpFile, Output) then
    begin
      if Pos('Microsoft.WindowsDesktop.App 10.0.', Output) > 0 then
        Result := True;
    end;
    DeleteFile(TmpFile);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  Response: Integer;
  PsCommand: String;
begin
  Result := '';

  if IsDotNet10Installed then
    Exit; // Already installed, proceed

  // Not installed — ask user
  Response := MsgBox(
    'Parallax Capture requires the .NET 10 Desktop Runtime.' + #13#10 + #13#10 +
    'Download and install it now? (~60 MB)' + #13#10 + #13#10 +
    'If you choose No, the installation will abort.',
    mbConfirmation, MB_YESNO);

  if Response = IDNO then
  begin
    Result := '.NET 10 Desktop Runtime is required to run Parallax Capture.' + #13#10 +
              'Installation aborted. Please install .NET 10 manually from https://dotnet.microsoft.com/download';
    Exit;
  end;

  // Single PowerShell command: download + run the official .NET install script
  PsCommand :=
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; ' +
    'Invoke-WebRequest ''https://dot.net/v1/dotnet-install.ps1'' -OutFile ''$env:TEMP\dotnet-install.ps1''; ' +
    '& ''$env:TEMP\dotnet-install.ps1'' -Channel 10.0 -Runtime windowsdesktop';

  if not Exec('powershell.exe',
       '-NoProfile -ExecutionPolicy Bypass -Command "' + PsCommand + '"',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Failed to launch .NET Runtime installer. PowerShell is required.';
    Exit;
  end;

  if ResultCode <> 0 then
  begin
    Result := '.NET 10 Runtime installation failed with code ' + IntToStr(ResultCode) + '.' + #13#10 +
              'Please install it manually from https://dotnet.microsoft.com/download';
    Exit;
  end;

  // Verify installation succeeded
  if not IsDotNet10Installed then
  begin
    Result := '.NET 10 Runtime installation completed but could not be verified.' + #13#10 +
              'Please restart the installer or install manually.';
    Exit;
  end;
end;
