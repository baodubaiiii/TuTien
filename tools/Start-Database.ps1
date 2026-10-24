$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$runtimePath = Join-Path $projectRoot '.runtime/mysql'
$dataPath = Join-Path $runtimePath 'data'
$privatePath = Join-Path $runtimePath 'admin.credential.xml'
$server = 'C:/xampp/mysql/bin/mysqld.exe'
$installer = 'C:/xampp/mysql/bin/mysql_install_db.exe'
function Test-DatabasePort {
    $probe = New-Object Net.Sockets.TcpClient
    try {
        $attempt = $probe.ConnectAsync('127.0.0.1', 3307)
        return $attempt.Wait(300) -and $probe.Connected
    } catch { return $false } finally { $probe.Dispose() }
}
if (-not (Test-Path -LiteralPath $server)) { throw 'MariaDB runtime not found. Configure the local database runtime first.' }
$pidPath = Join-Path $runtimePath 'mariadb.pid'
if (Test-Path -LiteralPath $pidPath) {
    $databaseProcessId = [int](Get-Content -LiteralPath $pidPath -Raw)
    $databaseProcess = Get-Process -Id $databaseProcessId -ErrorAction SilentlyContinue
    if ($databaseProcess -and $databaseProcess.Path -eq (Get-Item -LiteralPath $server).FullName -and (Test-DatabasePort)) { return }
}
$probe = New-Object Net.Sockets.TcpClient
try {
    $attempt = $probe.ConnectAsync('127.0.0.1', 3307)
    if ($attempt.Wait(500) -and $probe.Connected) { throw 'Port 3307 is occupied by another process.' }
} catch [AggregateException] { } finally { $probe.Dispose() }
New-Item -ItemType Directory -Path $runtimePath,(Join-Path $runtimePath 'tmp') -Force | Out-Null
$aliasPath = Join-Path ([IO.Path]::GetPathRoot($projectRoot)) 'TuTienMySql'
if (Test-Path -LiteralPath $aliasPath) {
    $alias = Get-Item -LiteralPath $aliasPath
    if ($alias.LinkType -ne 'Junction' -or [IO.Path]::GetFullPath($alias.Target) -ne [IO.Path]::GetFullPath($runtimePath)) { throw 'Unexpected database runtime alias.' }
} else {
    New-Item -ItemType Junction -Path $aliasPath -Target $runtimePath | Out-Null
}
if (-not (Test-Path -LiteralPath $dataPath)) {
    $passwordBytes = New-Object byte[] 24
    [Security.Cryptography.RandomNumberGenerator]::Fill($passwordBytes)
    $password = [Convert]::ToHexString($passwordBytes)
    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
    $credential = New-Object Management.Automation.PSCredential('root', $securePassword)
    $credential | Export-Clixml -LiteralPath $privatePath
    $null = & $installer "--datadir=$aliasPath/data" '--port=3307' "--password=$password" --silent
    if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed.' }
    Set-Content -LiteralPath (Join-Path $runtimePath 'fresh-instance') -Value '3307' -Encoding ascii
}
if (-not (Test-Path -LiteralPath $privatePath)) { throw 'Local database credential is missing. Existing data was preserved.' }
$iniPath = Join-Path $runtimePath 'my.ini'
$runtimeUnix = $aliasPath.Replace('\','/')
$dataUnix = $runtimeUnix + '/data'
@"
[mysqld]
basedir=C:/xampp/mysql
datadir=$dataUnix
port=3307
bind-address=127.0.0.1
skip-name-resolve
pid-file=$runtimeUnix/mariadb.pid
log-error=$runtimeUnix/server.log
tmpdir=$runtimeUnix/tmp
character-set-server=utf8mb4
collation-server=utf8mb4_bin
innodb-buffer-pool-size=64M
"@ | Set-Content -LiteralPath $iniPath -Encoding utf8
Start-Process -FilePath $server -ArgumentList @("--defaults-file=$runtimeUnix/my.ini") -WindowStyle Hidden | Out-Null
for ($attemptIndex = 0; $attemptIndex -lt 30; $attemptIndex++) {
    if ((Test-Path -LiteralPath $pidPath) -and (Test-DatabasePort)) { Write-Output 'TuTien MariaDB listening on 127.0.0.1:3307.'; return }
    Start-Sleep -Milliseconds 300
}
throw 'Local database did not start. See .runtime/mysql/server.log.'
