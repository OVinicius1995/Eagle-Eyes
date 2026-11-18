#define AppVersion GetEnv("EAGLE_EYES_VERSION")
#define PublishDir GetEnv("EAGLE_EYES_PUBLISH")
#define OutputDir GetEnv("EAGLE_EYES_INSTALLER_OUTPUT")
[Setup]
AppId={{A15849CE-44AD-4A1C-AF1F-E97B35A4E377}
AppName=Eagle Eyes
AppVersion={#AppVersion}
AppPublisher=Eagle Eyes
AppPublisherURL=https://github.com/OVinicius1995/Eagle-Eyes
DefaultDirName={localappdata}\Programs\Eagle Eyes
DefaultGroupName=Eagle Eyes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=EagleEyes-{#AppVersion}-win-x64-Setup
SetupIconFile=..\Assets\eagle-eyes.ico
UninstallDisplayIcon={app}\Assets\eagle-eyes.ico
AppMutex=Local\EagleEyes.Desktop
CloseApplications=no
RestartApplications=no
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
InfoBeforeFile=PREREQUISITES.pt-BR.txt
[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Eagle Eyes"; Filename: "{app}\EagleEyes.App.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\eagle-eyes.ico"
Name: "{autodesktop}\Eagle Eyes"; Filename: "{app}\EagleEyes.App.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\eagle-eyes.ico"; Tasks: desktopicon
[Run]
Filename: "{app}\EagleEyes.App.exe"; Description: "Abrir Eagle Eyes"; Flags: postinstall nowait skipifsilent unchecked
[Code]
function InitializeUninstall(): Boolean;
begin
  Result := not CheckForMutexes('Local\EagleEyes.Desktop');
  if (not Result) and (not UninstallSilent) then MsgBox('O Eagle Eyes está aberto. Salve sua gravação e saia do aplicativo antes de desinstalar.', mbInformation, MB_OK);
end;
