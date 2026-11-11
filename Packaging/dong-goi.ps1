<#
.SYNOPSIS
  Đóng gói Autumn Hotel thành bộ cài đặt Windows (Setup.exe) và bản portable (.zip).

.DESCRIPTION
  1. dotnet publish ứng dụng desktop (và cổng web) dạng một tệp exe tự chứa .NET 10, win-x64.
  2. Thay appsettings.json bằng bản dùng SQL Server Express LocalDB.
  3. Tải SqlLocalDB.msi (SQL Server 2022) để bộ cài tự cài LocalDB nếu máy chưa có.
  4. Biên dịch bộ cài bằng Inno Setup 6 (ISCC.exe) nếu máy có cài.
  Kết quả nằm trong Packaging\Output.

.EXAMPLE
  .\Packaging\dong-goi.ps1 -PhienBan 1.0.0
#>
param(
    [string]$PhienBan = '1.0.0',
    [switch]$BoQuaWeb,
    [string]$LocalDbUrl = 'https://download.microsoft.com/download/3/8/d/38de7036-2433-4207-8eae-06e247e17b25/SqlLocalDB.msi'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$thuMucDongGoi = $PSScriptRoot
$goc = Split-Path -Parent $thuMucDongGoi
$publish = Join-Path $goc 'publish'
$output = Join-Path $thuMucDongGoi 'Output'

function Chay-Publish([string]$project, [string]$dich) {
    & dotnet publish $project -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -p:Version=$PhienBan `
        -o $dich
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish thất bại: $project" }
}

Write-Host "==> Dọn thư mục publish và Output"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publish, $output | Out-Null

Write-Host "==> Publish ứng dụng desktop"
Chay-Publish (Join-Path $goc 'QuanLyKhachSan\QuanLyKhachSan.csproj') (Join-Path $publish 'app')
Copy-Item (Join-Path $thuMucDongGoi 'appsettings.app.json') (Join-Path $publish 'app\appsettings.json') -Force
foreach ($tep in @('Resources\hotel.png', 'Resources\logo.png', 'Resources\logo.ico')) {
    if (-not (Test-Path (Join-Path $publish "app\$tep"))) { throw "Thiếu $tep cạnh QuanLyKhachSan.exe (bị nhúng vào exe?)" }
}

if (-not $BoQuaWeb) {
    Write-Host "==> Publish cổng web tra cứu"
    Chay-Publish (Join-Path $goc 'QuanLyKhachSan.Web\QuanLyKhachSan.Web.csproj') (Join-Path $publish 'web')
    Copy-Item (Join-Path $thuMucDongGoi 'appsettings.web.json') (Join-Path $publish 'web\appsettings.json') -Force
}

Write-Host "==> Chuẩn bị SqlLocalDB.msi"
$msiCache = Join-Path $thuMucDongGoi 'SqlLocalDB.msi'
if (-not (Test-Path $msiCache)) {
    Write-Host "    Đang tải từ $LocalDbUrl"
    Invoke-WebRequest -Uri $LocalDbUrl -OutFile $msiCache -UseBasicParsing
}
Copy-Item $msiCache (Join-Path $publish 'SqlLocalDB.msi') -Force

Write-Host "==> Tạo bản portable (.zip)"
$zip = Join-Path $output "AutumnHotel-portable-$PhienBan.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
$tamZip = Join-Path $publish 'portable'
New-Item -ItemType Directory -Force -Path $tamZip | Out-Null
Copy-Item (Join-Path $publish 'app\*') $tamZip -Recurse
if (-not $BoQuaWeb) { Copy-Item (Join-Path $publish 'web') (Join-Path $tamZip 'Web') -Recurse }
Copy-Item (Join-Path $publish 'SqlLocalDB.msi') $tamZip
Copy-Item (Join-Path $thuMucDongGoi 'DOC-TRUOC.txt') $tamZip
Compress-Archive -Path (Join-Path $tamZip '*') -DestinationPath $zip
Remove-Item $tamZip -Recurse -Force

Write-Host "==> Biên dịch bộ cài đặt (Inno Setup)"
$iscc = $null
$lenh = Get-Command 'iscc.exe' -ErrorAction SilentlyContinue
if ($lenh) { $iscc = $lenh.Source }
foreach ($p in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")) {
    if (-not $iscc -and (Test-Path $p)) { $iscc = $p }
}
if ($iscc) {
    $thamSo = @("/DAppVersion=$PhienBan")
    if ($BoQuaWeb) { $thamSo += '/DKhongCoWeb=1' }
    & $iscc @thamSo (Join-Path $thuMucDongGoi 'AutumnHotel.iss')
    if ($LASTEXITCODE -ne 0) { throw 'ISCC thất bại' }
} else {
    Write-Warning 'Không tìm thấy Inno Setup 6 (ISCC.exe): bỏ qua bộ cài đặt, chỉ có bản portable. Cài tại https://jrsoftware.org/isdl.php rồi chạy lại.'
}

Write-Host ''
Write-Host 'Hoàn tất. Kết quả:' -ForegroundColor Green
Get-ChildItem $output | ForEach-Object { Write-Host ("  {0}  ({1:N1} MB)" -f $_.FullName, ($_.Length / 1MB)) }
