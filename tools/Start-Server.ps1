param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$localSdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { 'dotnet' }
function Check-Exit {
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE" }
}
if (-not $SkipBuild) {
    & $dotnet restore TuTien.slnx
    Check-Exit
    & $dotnet build TuTien.slnx -c Release --no-restore
    Check-Exit
    if (-not (Test-Path -LiteralPath 'appsettings.Local.json') -and -not $env:ConnectionStrings__Game) {
        & (Join-Path $PSScriptRoot 'Setup-Database.ps1')
    }
    if (Test-Path -LiteralPath '.runtime/mysql/admin.credential.xml') {
        & (Join-Path $PSScriptRoot 'Start-Database.ps1')
        & $dotnet run --project tools/DatabaseBuilder -c Release --no-build -- $projectRoot
        Check-Exit
    }
    & $dotnet run --project tools/ContentBuilder -c Release --no-build -- $projectRoot
    Check-Exit
}
if ($SkipBuild -and (Test-Path -LiteralPath '.runtime/mysql/admin.credential.xml')) {
    & (Join-Path $PSScriptRoot 'Start-Database.ps1')
}
& $dotnet run --project TuTien.Server.csproj -c Release --no-build -- --environment Development --Game:Root $projectRoot
Check-Exit
