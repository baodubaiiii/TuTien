$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
& (Join-Path $PSScriptRoot 'Start-Database.ps1')
$credential = Import-Clixml -LiteralPath (Join-Path $projectRoot '.runtime/mysql/admin.credential.xml')
$previousConnection = $env:TUTIEN_DB_ADMIN_CONNECTION
try {
    $adminPassword = $credential.GetNetworkCredential().Password
    $env:TUTIEN_DB_ADMIN_CONNECTION = "Server=127.0.0.1;Port=3307;User ID=root;Password=$adminPassword;"
    & (Join-Path $projectRoot '.tools/dotnet/dotnet.exe') run --project tools/DatabaseBuilder -c Release --no-build -- $projectRoot --migrate-progression
    if ($LASTEXITCODE -ne 0) { throw 'Progression migration failed.' }
    & (Join-Path $projectRoot '.tools/dotnet/dotnet.exe') run --project tools/ContentBuilder -c Release --no-build -- $projectRoot
    if ($LASTEXITCODE -ne 0) { throw 'CDN publish failed.' }
} finally {
    $env:TUTIEN_DB_ADMIN_CONNECTION = $previousConnection
    $adminPassword = $null
}
