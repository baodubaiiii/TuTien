$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$credential = Import-Clixml -LiteralPath (Join-Path $projectRoot '.runtime/mysql/admin.credential.xml')
$previousPassword = $env:MYSQL_PWD
try {
    $env:MYSQL_PWD = $credential.GetNetworkCredential().Password
    & 'C:/xampp/mysql/bin/mysqladmin.exe' --host=127.0.0.1 --port=3307 --user=root shutdown
    if ($LASTEXITCODE -ne 0) { throw 'Private database shutdown failed.' }
    $pidPath = Join-Path $projectRoot '.runtime/mysql/mariadb.pid'
    for ($attempt = 0; $attempt -lt 50 -and (Test-Path -LiteralPath $pidPath); $attempt++) {
        Start-Sleep -Milliseconds 200
    }
    if (Test-Path -LiteralPath $pidPath) { throw 'Database is still shutting down; data was preserved.' }
    Write-Output 'TuTien database stopped on port 3307.'
} finally { $env:MYSQL_PWD = $previousPassword }
