; Bộ cài đặt Autumn Hotel (Inno Setup 6). Thường được gọi từ dong-goi.ps1:
;   ISCC.exe /DAppVersion=1.0.0 AutumnHotel.iss
; Cần chạy dong-goi.ps1 trước để có thư mục ..\publish.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{8F3C2A51-7B4E-4C1A-9D3E-2B6A5F0C1D77}
AppName=Autumn Hotel
AppVersion={#AppVersion}
AppVerName=Autumn Hotel {#AppVersion}
AppPublisher=Nhóm 12 - Lập trình Windows
DefaultDirName={autopf}\Autumn Hotel
DefaultGroupName=Autumn Hotel
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=AutumnHotel-Setup-{#AppVersion}
SetupIconFile=..\QuanLyKhachSan\Resources\logo.ico
UninstallDisplayIcon={app}\QuanLyKhachSan.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Đầy đủ (ứng dụng quản lý + cổng web tra cứu)"
Name: "compact"; Description: "Chỉ ứng dụng quản lý"
Name: "custom"; Description: "Tùy chọn"; Flags: iscustom

[Components]
Name: "app"; Description: "Ứng dụng quản lý khách sạn"; Types: full compact custom; Flags: fixed
#ifndef KhongCoWeb
Name: "web"; Description: "Cổng web tra cứu cho khách (mã QR)"; Types: full
#endif

[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng trên Desktop"; GroupDescription: "Biểu tượng:"

[Files]
Source: "..\publish\app\*"; DestDir: "{app}"; Excludes: "appsettings.json"; Components: app; Flags: ignoreversion recursesubdirs createallsubdirs
; Giữ cấu hình đã sửa khi cài bản mới đè lên.
Source: "..\publish\app\appsettings.json"; DestDir: "{app}"; Components: app; Flags: onlyifdoesntexist
#ifndef KhongCoWeb
Source: "..\publish\web\*"; DestDir: "{app}\Web"; Excludes: "appsettings.json"; Components: web; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\web\appsettings.json"; DestDir: "{app}\Web"; Components: web; Flags: onlyifdoesntexist
#endif
Source: "DOC-TRUOC.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\SqlLocalDB.msi"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: CanCaiLocalDb

[Icons]
Name: "{autoprograms}\Autumn Hotel"; Filename: "{app}\QuanLyKhachSan.exe"
Name: "{autodesktop}\Autumn Hotel"; Filename: "{app}\QuanLyKhachSan.exe"; Tasks: desktopicon
#ifndef KhongCoWeb
Name: "{autoprograms}\Autumn Hotel - Cổng web tra cứu"; Filename: "{app}\Web\QuanLyKhachSan.Web.exe"; WorkingDir: "{app}\Web"; Components: web
#endif

[Run]
Filename: "{app}\QuanLyKhachSan.exe"; Description: "Mở Autumn Hotel"; Flags: nowait postinstall skipifsilent

[Code]
var
  CanCaiLocalDbFlag: Boolean;

{ LocalDB 2019 (15.0) trở lên. Script dùng CREATE OR ALTER nên bản quá cũ không chạy được. }
function DaCoLocalDb(): Boolean;
var
  Names: TArrayOfString;
  I, P, Major: Integer;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      P := Pos('.', Names[I]);
      if P > 0 then
        Major := StrToIntDef(Copy(Names[I], 1, P - 1), 0)
      else
        Major := StrToIntDef(Names[I], 0);
      if Major >= 15 then
        Result := True;
    end;
end;

function InitializeSetup(): Boolean;
begin
  CanCaiLocalDbFlag := not DaCoLocalDb();
  Result := True;
end;

function CanCaiLocalDb(): Boolean;
begin
  Result := CanCaiLocalDbFlag;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep <> ssPostInstall then
    Exit;

  if CanCaiLocalDbFlag then
  begin
    WizardForm.StatusLabel.Caption := 'Đang cài SQL Server Express LocalDB (có thể mất vài phút)...';
    if (not Exec(ExpandConstant('{sys}\msiexec.exe'),
          '/i "' + ExpandConstant('{tmp}\SqlLocalDB.msi') + '" /qn /norestart IACCEPTSQLLOCALDBLICENSETERMS=YES',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode))
       or ((ResultCode <> 0) and (ResultCode <> 3010)) then
    begin
      SuppressibleMsgBox('Không cài được SQL Server Express LocalDB (mã lỗi ' + IntToStr(ResultCode) + ').' + #13#10 +
        'Autumn Hotel cần LocalDB để lưu dữ liệu. Hãy cài thủ công SqlLocalDB.msi (SQL Server 2022) từ trang của Microsoft rồi mở lại ứng dụng.',
        mbError, MB_OK, IDOK);
      Exit;
    end;
  end;

  { Tạo sẵn database cho người dùng đã chạy bộ cài (không phải tài khoản admin nếu khác người). }
  WizardForm.StatusLabel.Caption := 'Đang khởi tạo cơ sở dữ liệu (lần đầu có thể mất 1-2 phút)...';
  ExecAsOriginalUser(ExpandConstant('{app}\QuanLyKhachSan.exe'), '--khoi-tao', ExpandConstant('{app}'),
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  { Lỗi ở bước này không chặn cài đặt: ứng dụng sẽ tự thử lại và báo lỗi chi tiết khi mở. }
end;
