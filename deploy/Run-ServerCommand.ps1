<#
  Chạy lệnh quản trị của NHNhaTro trên máy chủ qua WinRM, dùng cấu hình Production đang có trên máy chủ.

  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command seed-demo   # nạp dữ liệu nghiệm thu
  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command clear-data  # xóa dữ liệu nghiệp vụ
  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command import-tenants -File "temps\danh sách tiền phòng trọ.xlsx"  # xóa dữ liệu thử, nhập khách thật
  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command seed-billing-test  # thêm 2 khách thử (vào ngày 01 và 20 tháng trước) có phiếu kỳ đầu + kỳ sau
#>
param(
    [Parameter(Mandatory)][ValidateSet('seed-demo', 'clear-data', 'seed-billing-test', 'import-tenants')][string]$Command,
    [string]$File,
    [string]$Server = '192.168.1.89',
    [string]$SitePath = 'C:\inetpub\wwwroot\NhaTroAnCu',
    [string]$AppPool = 'NHNhaTro',
    [string]$CredentialFile = "$env:USERPROFILE\.nhnhatro-server-cred.xml"
)
$ErrorActionPreference = 'Stop'
$cred = Import-Clixml $CredentialFile
$remoteFile = $null
if ($File) {
    if (-not (Test-Path $File)) { throw "Không thấy file $File" }
    $remoteFile = 'C:\Setup\' + [IO.Path]::GetFileName($File)
    $s = New-PSSession -ComputerName $Server -Credential $cred
    try {
        Invoke-Command -Session $s -ScriptBlock { New-Item -ItemType Directory -Force C:\Setup | Out-Null }
        Copy-Item -LiteralPath (Resolve-Path -LiteralPath $File) -Destination $remoteFile -ToSession $s -Force
    } finally { Remove-PSSession $s }
}
Invoke-Command -ComputerName $Server -Credential $cred -ArgumentList $SitePath, $AppPool, $Command, $remoteFile -ScriptBlock {
    param($site, $pool, $cmd, $file)
    Set-Location $site
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:Logging__LogLevel__Default = 'Warning'
    $cmdArgs = @("--$cmd"); if ($file) { $cmdArgs += $file }
    & .\NHNhaTro.exe @cmdArgs 2>&1 | Where-Object { $_ -notmatch '^\s*(warn|info):' -and $_ -notmatch '^\s{6}' } | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    # File tải lên do tài khoản quản trị tạo: cấp lại quyền đọc/ghi cho app pool
    $uploads = (Get-Content "$site\appsettings.Production.json" -Raw | ConvertFrom-Json).Storage.UploadsPath
    if ($uploads -and (Test-Path $uploads)) { icacls $uploads /grant "IIS AppPool\${pool}:(OI)(CI)M" /T /Q | Out-Null }
    "Mã thoát: $code"
}
