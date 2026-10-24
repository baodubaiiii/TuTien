$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
if (Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue) {
    throw 'Stop the local game server before resetting demo data.'
}
& (Join-Path $PSScriptRoot 'Start-Database.ps1')
$credential = Import-Clixml -LiteralPath (Join-Path $projectRoot '.runtime/mysql/admin.credential.xml')
$adminPassword = $credential.GetNetworkCredential().Password
$previousConnection = $env:TUTIEN_DB_ADMIN_CONNECTION
$previousPassword = $env:MYSQL_PWD
try {
    $env:MYSQL_PWD = $adminPassword
    $backup = Join-Path $projectRoot ('artifacts/before-reference-layout/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = 'C:/xampp/mysql/bin/mysqldump.exe'
    $start.Arguments = '--host=127.0.0.1 --port=3307 --user=root --default-character-set=utf8mb4 --single-transaction --skip-lock-tables --databases tutien'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = New-Object Text.UTF8Encoding($false)
    $process = [Diagnostics.Process]::Start($start)
    $sql = $process.StandardOutput.ReadToEnd()
    $errorText = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw 'Database backup failed. Migration was not applied.' }
    [IO.File]::WriteAllText((Join-Path $backup 'tutien.sql'), $sql, (New-Object Text.UTF8Encoding($false)))
    $env:TUTIEN_DB_ADMIN_CONNECTION = "Server=127.0.0.1;Port=3307;User ID=root;Password=$adminPassword;"
    $dotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
    & $dotnet run --project tools/DatabaseBuilder -c Release --no-build -- $projectRoot --reset-demo
    if ($LASTEXITCODE -ne 0) { throw 'Demo reset failed.' }
    & $dotnet run --project tools/ContentBuilder -c Release --no-build -- $projectRoot
    if ($LASTEXITCODE -ne 0) { throw 'CDN publish failed.' }
    $keys = Join-Path $projectRoot '.runtime/character-creation/keys'
    if (Test-Path -LiteralPath $keys) {
        foreach ($key in Get-ChildItem -LiteralPath $keys -Filter 'key-*.xml' -File) {
            Copy-Item -LiteralPath $key.FullName -Destination $backup
            Remove-Item -LiteralPath $key.FullName
        }
    }
    Write-Output 'Demo accounts and characters reset; reference schema and CDN applied. SQL snapshot is under artifacts/before-reference-layout.'
} finally {
    $env:MYSQL_PWD = $previousPassword
    $env:TUTIEN_DB_ADMIN_CONNECTION = $previousConnection
    $adminPassword = $null
}
