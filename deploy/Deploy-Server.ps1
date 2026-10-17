<#
  Publish NHNhaTro và cập nhật lên máy chủ IIS qua WinRM (PowerShell Remoting).

  Không chứa mật khẩu nào:
  - Tài khoản quản trị máy chủ đọc từ file mã hóa (DPAPI) do người dùng tự tạo một lần:
        Get-Credential | Export-Clixml "$env:USERPROFILE\.nhnhatro-server-cred.xml"
  - appsettings.Production.json (chuỗi kết nối) đã nằm sẵn trên máy chủ và KHÔNG bị ghi đè.
    Thêm -WriteConnectionString để tạo lại nó từ User Secrets của máy này.

  Cách chạy (từ thư mục repo):  powershell -ExecutionPolicy Bypass -File deploy\Deploy-Server.ps1
#>
param(
    [string]$Server = '192.168.1.89',
    [string]$SitePath = 'C:\inetpub\wwwroot\NhaTroAnCu',
    [string]$DataPath = 'D:\NHNhaTroData',
    [string]$AppPool = 'NHNhaTro',
    [string]$CredentialFile = "$env:USERPROFILE\.nhnhatro-server-cred.xml",
    [switch]$WriteConnectionString
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$work = Join-Path $env:TEMP 'nhnhatro-deploy'
$out = Join-Path $work 'publish'
$zip = Join-Path $work 'nhnhatro-publish.zip'

if (-not (Test-Path $CredentialFile)) { throw "Chưa có file tài khoản máy chủ $CredentialFile (xem đầu script)." }
$cred = Import-Clixml $CredentialFile

Write-Host '1/4 Publish Release win-x64...'
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
dotnet publish (Join-Path $repo 'NHNhaTro\NHNhaTro.csproj') -c Release -r win-x64 --self-contained false -o $out -nologo -v:q
if ($LASTEXITCODE -ne 0) { throw 'Publish lỗi.' }
Remove-Item (Join-Path $out 'appsettings.Development.json') -ErrorAction SilentlyContinue
Compress-Archive -Path "$out\*" -DestinationPath $zip

$cs = $null
if ($WriteConnectionString) {
    $secretsFile = "$env:APPDATA\Microsoft\UserSecrets\aspnet-NHNhaTro-81a32014-bd27-4e28-85be-ea325cd460d1\secrets.json"
    $cs = (Get-Content $secretsFile -Raw | ConvertFrom-Json).'ConnectionStrings:DefaultConnection' -replace 'Server=WIN-2VO4M17R0NR,9898', 'Server=localhost,9898'
}

Write-Host '2/4 Chép gói lên máy chủ...'
$s = New-PSSession -ComputerName $Server -Credential $cred
try {
    Invoke-Command -Session $s -ScriptBlock { New-Item -ItemType Directory -Force C:\Setup | Out-Null }
    Copy-Item $zip -Destination C:\Setup\nhnhatro-publish.zip -ToSession $s -Force

    Write-Host '3/4 Dừng app pool, cập nhật file...'
    Invoke-Command -Session $s -ArgumentList $SitePath, $DataPath, $AppPool, $cs -ScriptBlock {
        param($site, $data, $pool, $cs)
        Import-Module WebAdministration
        if ((Get-WebAppPoolState $pool).Value -ne 'Stopped') { Stop-WebAppPool $pool; Start-Sleep 3 }
        Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value ''
        Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.loadUserProfile -Value $true
        New-Item -ItemType Directory -Force "$data\Uploads", "$site\logs" | Out-Null
        Expand-Archive C:\Setup\nhnhatro-publish.zip -DestinationPath $site -Force
        $acct = "IIS AppPool\$pool"
        if ($cs) {
            $json = [ordered]@{ ConnectionStrings = @{ DefaultConnection = $cs }; Storage = @{ UploadsPath = "$data\Uploads" } } | ConvertTo-Json -Depth 3
            [IO.File]::WriteAllText("$site\appsettings.Production.json", $json, (New-Object Text.UTF8Encoding $false))
            icacls "$site\appsettings.Production.json" /inheritance:r /grant:r "Administrators:F" "SYSTEM:F" "${acct}:R" /Q | Out-Null
        }
        if (-not (Test-Path "$site\appsettings.Production.json")) { Write-Warning 'Thiếu appsettings.Production.json trên máy chủ: chạy lại với -WriteConnectionString.' }
        icacls "$data\Uploads" /grant "${acct}:(OI)(CI)M" /T /Q | Out-Null
        icacls "$site\logs" /grant "${acct}:(OI)(CI)M" /Q | Out-Null
        Start-WebAppPool $pool
    }
} finally { Remove-PSSession $s }

Write-Host '4/4 Kiểm tra...'
$host_ = (Invoke-Command -ComputerName $Server -Credential $cred -ArgumentList $AppPool -ScriptBlock {
    param($pool) Import-Module WebAdministration
    (Get-Website | ? applicationPool -eq $pool | Select -First 1).bindings.Collection | ? protocol -eq 'http' | Select -First 1 -Expand bindingInformation
}).Split(':')[-1]
$r = Invoke-WebRequest "http://$Server/Account/Login" -Headers @{ Host = $host_ } -UseBasicParsing -TimeoutSec 90
Write-Host ("http://{0}/Account/Login -> {1}" -f $host_, $r.StatusCode)
