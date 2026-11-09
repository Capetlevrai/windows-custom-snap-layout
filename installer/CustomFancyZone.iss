; Installeur de CustomFancyZone (Inno Setup 6). Construit par release.ps1, qui passe /DAppVersion et /DSourceExe.
; Installation par utilisateur, sans droits administrateur, dans %LOCALAPPDATA%\Programs\CustomFancyZone.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\dist\portable\CustomFancyZone.exe"
#endif

[Setup]
AppId={{6C1E2B57-3F0A-4C55-9E2D-CF7A1B0D9A41}
AppName=CustomFancyZone
AppVersion={#AppVersion}
AppVerName=CustomFancyZone {#AppVersion}
AppPublisher=Capetlevrai
AppPublisherURL=https://github.com/Capetlevrai/windows-custom-snap-layout
AppSupportURL=https://github.com/Capetlevrai/windows-custom-snap-layout/issues
DefaultDirName={localappdata}\Programs\CustomFancyZone
DefaultGroupName=CustomFancyZone
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=CustomFancyZone-Setup-{#AppVersion}
SetupIconFile=..\src\app.ico
UninstallDisplayIcon={app}\CustomFancyZone.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "autostart"; Description: "Lancer CustomFancyZone au démarrage de Windows"; GroupDescription: "Options :"
Name: "desktopicon"; Description: "Créer une icône sur le Bureau"; GroupDescription: "Options :"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\CustomFancyZone"; Filename: "{app}\CustomFancyZone.exe"
Name: "{userprograms}\CustomFancyZone - Modifier les dispositions"; Filename: "{app}\layouts.json"
Name: "{userdesktop}\CustomFancyZone"; Filename: "{app}\CustomFancyZone.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CustomFancyZone"; \
    ValueData: """{app}\CustomFancyZone.exe"""; Tasks: autostart

[Run]
Filename: "{app}\CustomFancyZone.exe"; Description: "Lancer CustomFancyZone maintenant"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\CustomFancyZone.exe"; Parameters: "--quit"; Flags: runhidden waituntilterminated; RunOnceId: "QuitApp"

[UninstallDelete]
Type: files; Name: "{app}\layouts.json"
Type: files; Name: "{app}\CustomFancyZone.log"
Type: dirifempty; Name: "{app}"

[Code]
// Ferme proprement une version déjà lancée avant de remplacer l'exe (layouts.json est conservé).
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  Exe: String;
begin
  Result := '';
  Exe := ExpandConstant('{app}\CustomFancyZone.exe');
  if FileExists(Exe) then
  begin
    Exec(Exe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1500);
  end;
end;

// À la désinstallation, retire aussi le lancement automatique s'il a été activé depuis le menu de l'icône,
// mais seulement s'il pointe vers cette installation (pas vers une autre copie de l'outil).
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Value: String;
begin
  if (CurUninstallStep = usUninstall) and
     RegQueryStringValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'CustomFancyZone', Value) and
     (Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Value)) > 0) then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'CustomFancyZone');
end;
