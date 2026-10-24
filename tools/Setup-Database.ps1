$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
& (Join-Path $PSScriptRoot 'Start-Database.ps1')
$credential = Import-Clixml -LiteralPath (Join-Path $projectRoot '.runtime/mysql/admin.credential.xml')
$adminPassword = $credential.GetNetworkCredential().Password
$previousConnection = $env:TUTIEN_DB_ADMIN_CONNECTION
try {
    $freshInstance = Join-Path $projectRoot '.runtime/mysql/fresh-instance'
    if (Test-Path -LiteralPath $freshInstance) {
        $previousMysqlPassword = $env:MYSQL_PWD
        try {
            $env:MYSQL_PWD = $adminPassword
            $client = 'C:/xampp/mysql/bin/mysql.exe'
            $dataDirectory = (& $client --host=127.0.0.1 --port=3307 --user=root --batch --raw --skip-column-names --execute='SELECT @@datadir').Trim()
            $expectedDirectory = (Join-Path ([IO.Path]::GetPathRoot($projectRoot)) 'TuTienMySql/data').Replace([char]92, [char]47).TrimEnd('/')
            if ($dataDirectory.Replace([char]92, [char]47).TrimEnd('/') -ne $expectedDirectory) { throw 'Unexpected fresh database directory.' }
            $check = & $client --host=127.0.0.1 --port=3307 --user=root --batch --skip-column-names --execute='CHECK TABLE mysql.db'
            if ($LASTEXITCODE -ne 0) { throw 'Private database check failed.' }
            if (-not ($check -match 'status\s+OK$')) {
                $null = & $client --host=127.0.0.1 --port=3307 --user=root --batch --skip-column-names --execute='REPAIR TABLE mysql.db'
                if ($LASTEXITCODE -ne 0) { throw 'Private database initialization failed.' }
            }
            Remove-Item -LiteralPath $freshInstance
        } finally { $env:MYSQL_PWD = $previousMysqlPassword }
    }
    $env:TUTIEN_DB_ADMIN_CONNECTION = "Server=127.0.0.1;Port=3307;User ID=root;Password=$adminPassword;"
    $localSdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
    $dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { 'dotnet' }
    & $dotnet run --project tools/DatabaseBuilder -c Release --no-build -- $projectRoot --create
    if ($LASTEXITCODE -ne 0) { throw 'Database setup failed.' }
    & $dotnet run --project tools/ContentBuilder -c Release --no-build -- $projectRoot
    if ($LASTEXITCODE -ne 0) { throw 'CDN publish failed.' }
} finally {
    $env:TUTIEN_DB_ADMIN_CONNECTION = $previousConnection
    $adminPassword = $null
}
