<#
  Chạy lệnh quản trị của NHNhaTro trên máy chủ qua WinRM, dùng cấu hình Production đang có trên máy chủ.

  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command seed-demo   # nạp dữ liệu nghiệm thu
  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command clear-data  # xóa dữ liệu nghiệp vụ
  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command import-tenants -File "temps\danh sách tiền phòng trọ.xlsx"  # xóa dữ liệu thử, nhập khách thật
  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command seed-billing-test  # thêm 2 khách thử (vào ngày 01 và 20 tháng trước) có phiếu kỳ đầu + kỳ sau
  powershell -ExecutionPolicy Bypass -File deploy\Run-ServerCommand.ps1 -Command setup-horn -File a.pdf,b.pdf  # nhập HĐ công ty Horn + bản scan (một lần)
#>
param(
    [Parameter(Mandatory)][ValidateSet('seed-demo', 'clear-data', 'seed-billing-test', 'import-tenants', 'setup-horn')][string]$Command,
    [string[]]$File,
    [string]$Server = '192.168.1.89',
    [string]$SitePath = 'C:\inetpub\wwwroot\NhaTroAnCu',
    [string]$AppPool = 'NHNhaTro',
    [string]$CredentialFile = "$env:USERPROFILE\.nhnhatro-server-cred.xml"
)
$ErrorActionPreference = 'Stop'
$cred = Import-Clixml $CredentialFile
$remoteFiles = @()
# Gọi qua "powershell -File" thì "a.pdf,b.pdf" đến đây thành một chuỗi: tách theo dấu phẩy.
$File = @($File | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
if ($File) {
    $s = New-PSSession -ComputerName $Server -Credential $cred
    try {
        Invoke-Command -Session $s -ScriptBlock { New-Item -ItemType Directory -Force C:\Setup | Out-Null }
        foreach ($f in $File) {
            if (-not (Test-Path -LiteralPath $f)) { throw "Không thấy file $f" }
            $remote = 'C:\Setup\' + [IO.Path]::GetFileName($f)
            Copy-Item -LiteralPath (Resolve-Path -LiteralPath $f) -Destination $remote -ToSession $s -Force
            $remoteFiles += $remote
        }
    } finally { Remove-PSSession $s }
}
Invoke-Command -ComputerName $Server -Credential $cred -ArgumentList $SitePath, $AppPool, $Command, $remoteFiles -ScriptBlock {
    param($site, $pool, $cmd, $files)
    Set-Location $site
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:Logging__LogLevel__Default = 'Warning'
    $cmdArgs = @("--$cmd") + @($files | Where-Object { $_ })
    & .\NHNhaTro.exe @cmdArgs 2>&1 | Where-Object { $_ -notmatch '^\s*(warn|info):' -and $_ -notmatch '^\s{6}' } | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    # File tải lên do tài khoản quản trị tạo: cấp lại quyền đọc/ghi cho app pool
    $uploads = (Get-Content "$site\appsettings.Production.json" -Raw | ConvertFrom-Json).Storage.UploadsPath
    if ($uploads -and (Test-Path $uploads)) { icacls $uploads /grant "IIS AppPool\${pool}:(OI)(CI)M" /T /Q | Out-Null }
    "Mã thoát: $code"
}
