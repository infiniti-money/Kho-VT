#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#define MyAppName "HISA - Quản lý kho vật tư"
#define MyAppPublisher "Infiniti Seller"
#define MyAppExeName "HISA Quan Ly Kho.exe"

[Setup]
AppId={{3e525c27-4c6d-4b32-aec8-67c1687a9717}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/infiniti-money/Kho-VT
AppSupportURL=https://github.com/infiniti-money/Kho-VT/issues
AppUpdatesURL=https://github.com/infiniti-money/Kho-VT/releases
DefaultDirName={autopf}\HISA\QuanLyKho
DefaultGroupName=HISA - Quản lý kho vật tư
OutputDir=..\installer-output
OutputBaseFilename=HISA-KhoVT-Setup
SetupIconFile=..\Assets\hisa.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoProductName={#MyAppName}
VersionInfoDescription=Bộ cài HISA quản lý kho
WizardStyle=modern
WizardSizePercent=105
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes
ShowLanguageDialog=no
UsePreviousAppDir=yes
UsePreviousLanguage=yes
MinVersion=10.0
ChangesAssociations=no
SetupLogging=yes

[Languages]
Name: "vi"; MessagesFile: "compiler:Default.isl,Vietnamese.isl"

[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng HISA trên màn hình Desktop"; GroupDescription: "Tùy chọn biểu tượng:"

[Files]
Source: "..\publish-reconciled\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\HISA - Quản lý kho vật tư"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\HISA - Quản lý kho vật tư"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Interactive installation offers to open HISA. Silent installation automatically
; reopens HISA as the original user. No warehouse workbook is part of this installer.
Filename: "{app}\{#MyAppExeName}"; Description: "Khởi động HISA"; Flags: nowait postinstall skipifsilent runasoriginaluser
Filename: "{app}\{#MyAppExeName}"; Flags: nowait runasoriginaluser; Check: WizardSilent

