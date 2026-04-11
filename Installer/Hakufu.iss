; ============================================================================
;  Hakufu — Inno Setup 6 installer script
;
;  Compilar manualmente:
;    ISCC.exe Installer\Hakufu.iss
;
;  Compilar con versión específica:
;    ISCC.exe Installer\Hakufu.iss /DAppVersion=0.2.0
;
;  O usar el script de build incluido:
;    .\Build-Installer.ps1
;    .\Build-Installer.ps1 -Version "0.2.0"
;
;  Salida: output\Hakufu-<version>-Setup.exe
; ============================================================================

; ── Versión (sobreescribible desde CLI con /DAppVersion=x.x.x) ─────────────
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

; ── Constantes ───────────────────────────────────────────────────────────────
#define AppName       "Hakufu"
#define AppPublisher  "Daniel Poza"
#define AppExeName    "Hakufu.exe"
#define AppURL        "https://github.com/dap0ry/Hakufu"
#define SourceDir     "..\publish"

; IMPORTANTE: Este GUID identifica la app en el registro de Windows.
; NO cambiar entre versiones — Inno Setup lo usa para detectar instalaciones
; previas y realizar actualizaciones sin necesidad de desinstalar antes.
#define AppId "{6D4F3E12-8A1B-4C2D-9E5F-7B0A2C1D3E4F}"

; ── [Setup] ──────────────────────────────────────────────────────────────────
[Setup]
AppId={{#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases

; Directorio de instalación (autopf = Program Files x64 en sistemas de 64 bits)
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; Arquitectura
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Windows 10 1809+ (10.0.17763) requerido por .NET 10 WPF
MinVersion=10.0.17763

; Privilegios de administrador (necesario para instalar en Program Files)
PrivilegesRequired=admin

; Cerrar la app si está en ejecución antes de actualizar
CloseApplications=yes
CloseApplicationsFilter=*Hakufu.exe,*updater.exe
RestartApplications=no

; Aspecto del asistente
WizardStyle=modern
SetupIconFile=

; Compresión LZMA2 máxima para menor tamaño del instalador
Compression=lzma2/ultra64
SolidCompression=yes

; Iconos de desinstalación en Panel de Control
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}

; Salida del instalador
OutputDir=..\output
OutputBaseFilename=Hakufu-{#AppVersion}-Setup

; ── [Languages] ──────────────────────────────────────────────────────────────
[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

; ── [Tasks] ──────────────────────────────────────────────────────────────────
[Tasks]
; Acceso directo en el escritorio (marcado por defecto)
Name: "desktopicon"; Description: "Crear acceso directo en el {cm:DesktopName}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checked

; ── [Files] ──────────────────────────────────────────────────────────────────
[Files]
; Todos los archivos publicados por dotnet publish
; · ignoreversion  → sobreescribe sin preguntar (permite actualizar)
; · recursesubdirs → incluye subcarpetas (localizaciones, etc.)
; · Excludes       → excluye artefactos de debug y MSIX
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,AppxManifest.xml,Images,*.msix"

; ── [Icons] ──────────────────────────────────────────────────────────────────
[Icons]
; Menú inicio
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Comment: "Gestor de manga offline"
Name: "{group}\Desinstalar {#AppName}";  Filename: "{uninstallexe}"

; Escritorio (solo si el usuario marcó la tarea)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Comment: "Gestor de manga offline"; Tasks: desktopicon

; ── [Run] — Lanzar la app al terminar la instalación ─────────────────────────
[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar {#AppName}"; Flags: nowait postinstall skipifsilent

; ── [Code] ───────────────────────────────────────────────────────────────────
[Code]

{ ─────────────────────────────────────────────────────────────────────────────
  Detecta si hay una versión anterior instalada con el mismo AppId.
  Si la hay, la desinstala silenciosamente antes de instalar la nueva
  (sin borrar datos de usuario en AppData\Roaming\Hakufu).
  ───────────────────────────────────────────────────────────────────────────── }
function GetInstalledVersion(out UninstallExe: String): Boolean;
var
  RegKey: String;
begin
  Result := False;
  RegKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1';
  if RegQueryStringValue(HKLM, RegKey, 'UninstallString', UninstallExe) then
    Result := True
  else if RegQueryStringValue(HKCU, RegKey, 'UninstallString', UninstallExe) then
    Result := True;
end;

function InitializeSetup(): Boolean;
var
  UninstallExe:  String;
  ResultCode:    Integer;
  Msg:           String;
begin
  Result := True;

  if GetInstalledVersion(UninstallExe) then
  begin
    { Desinstalar la versión anterior en silencio (/SILENT /NORESTART)        }
    { El desinstalador NO borra AppData\Roaming\Hakufu (datos del usuario).   }
    Exec(RemoveQuotes(UninstallExe), '/SILENT /NORESTART', '', SW_HIDE,
         ewWaitUntilTerminated, ResultCode);
  end;
end;

{ ─────────────────────────────────────────────────────────────────────────────
  Mensaje informativo al desinstalar: los datos del usuario se conservan.
  ───────────────────────────────────────────────────────────────────────────── }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    MsgBox('Hakufu ha sido desinstalado.' + #13#10 + #13#10 +
           'Tus datos (mangas, historial, configuración) se han conservado en:' + #13#10 +
           '%APPDATA%\Hakufu' + #13#10 + #13#10 +
           'Puedes eliminarlos manualmente si ya no los necesitas.',
           mbInformation, MB_OK);
end;
