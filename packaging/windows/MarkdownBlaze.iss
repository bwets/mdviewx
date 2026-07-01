; Inno Setup script for MarkdownBlaze (per-user install, registers .md/.markdown association).
; Version + publish dir are passed on the command line by the release workflow:
;   ISCC /DMyAppVersion=1.0.5 /DPublishDir=<abs path to publish\win-x64> MarkdownBlaze.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\publish\win-x64"
#endif
#define MyAppName "MarkdownBlaze"
#define MyAppExe "MarkdownBlaze.exe"

[Setup]
AppId={{8B5E2A4C-6F1D-4E9A-9C3B-2D7A1F0E5B66}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Gregory Kieffer
AppPublisherURL=https://github.com/bwets/MarkdownBlaze
DefaultDirName={localappdata}\Programs\MarkdownBlaze
DefaultGroupName=MarkdownBlaze
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExe}
OutputBaseFilename=MarkdownBlaze-{#MyAppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ChangesAssociations=yes
; So `MarkdownBlaze` can be run from any terminal, the install dir is added to the user PATH.
ChangesEnvironment=yes

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Icons]
Name: "{group}\MarkdownBlaze"; Filename: "{app}\{#MyAppExe}"
Name: "{userdesktop}\MarkdownBlaze"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Registry]
; ProgId + add MarkdownBlaze as a handler for .md / .markdown (per-user; user can set it as default).
Root: HKCU; Subkey: "Software\Classes\MarkdownBlaze.Document"; ValueType: string; ValueName: ""; ValueData: "Markdown Document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\MarkdownBlaze.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExe},0"
Root: HKCU; Subkey: "Software\Classes\MarkdownBlaze.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: string; ValueName: "MarkdownBlaze.Document"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.markdown\OpenWithProgids"; ValueType: string; ValueName: "MarkdownBlaze.Document"; ValueData: ""; Flags: uninsdeletevalue
; Global alias: add the install dir to the user PATH so `MarkdownBlaze` works from any terminal.
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Check: NeedsAddPath(ExpandConstant('{app}'))

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch MarkdownBlaze"; Flags: nowait postinstall skipifsilent

[Code]
{ True when the given dir is not already present in the user PATH (avoids duplicate entries). }
function NeedsAddPath(Param: string): Boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
  begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Uppercase(Param) + ';', ';' + Uppercase(OrigPath) + ';') = 0;
end;

{ Remove the install dir from the user PATH on uninstall. }
procedure RemovePath(Param: string);
var
  OrigPath: string;
  P: Integer;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
    exit;
  OrigPath := ';' + OrigPath + ';';
  P := Pos(';' + Uppercase(Param) + ';', Uppercase(OrigPath));
  if P = 0 then exit;
  Delete(OrigPath, P, Length(Param) + 1);
  if (Length(OrigPath) > 0) and (OrigPath[1] = ';') then Delete(OrigPath, 1, 1);
  if (Length(OrigPath) > 0) and (OrigPath[Length(OrigPath)] = ';') then Delete(OrigPath, Length(OrigPath), 1);
  RegWriteExpandStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemovePath(ExpandConstant('{app}'));
end;
