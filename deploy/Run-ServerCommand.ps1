<#
  Chạy lệnh quản trị của NHNhaTro trên máy chủ qua WinRM, dùng cấu hình Production đang có trên máy chủ.

  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command seed-demo   # nạp dữ liệu nghiệm thu
  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command clear-data  # xóa dữ liệu nghiệp vụ
  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command seed-billing-test  # thêm 1 khách thử có phiếu kỳ đầu + kỳ 2
#>
param(
    [Parameter(Mandatory)][ValidateSet('seed-demo', 'clear-data', 'seed-billing-test')][string]$Command,
    [string]$Server = '192.168.1.89',
    [string]$SitePath = 'C:\inetpub\wwwroot\NhaTroAnCu',
    [string]$AppPool = 'NHNhaTro',
    [string]$CredentialFile = "$env:USERPROFILE\.nhnhatro-server-cred.xml"
)
$ErrorActionPreference = 'Stop'
$cred = Import-Clixml $CredentialFile
Invoke-Command -ComputerName $Server -Credential $cred -ArgumentList $SitePath, $AppPool, $Command -ScriptBlock {
    param($site, $pool, $cmd)
    Set-Location $site
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:Logging__LogLevel__Default = 'Warning'
    & .\NHNhaTro.exe "--$cmd" 2>&1 | Where-Object { $_ -notmatch '^\s*(warn|info):' -and $_ -notmatch '^\s{6}' } | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    # File tải lên do tài khoản quản trị tạo: cấp lại quyền đọc/ghi cho app pool
    $uploads = (Get-Content "$site\appsettings.Production.json" -Raw | ConvertFrom-Json).Storage.UploadsPath
    if ($uploads -and (Test-Path $uploads)) { icacls $uploads /grant "IIS AppPool\${pool}:(OI)(CI)M" /T /Q | Out-Null }
    "Mã thoát: $code"
}
