$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$localSdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { 'dotnet' }
function Check-Exit {
    if ($LASTEXITCODE -ne 0) { throw "Verification failed with exit code $LASTEXITCODE" }
}
& $dotnet restore TuTien.slnx
Check-Exit
& $dotnet build TuTien.slnx -c Release --no-restore
Check-Exit
& $dotnet test tests/TuTien.Tests -c Release --no-build
Check-Exit
& $dotnet format whitespace TuTien.slnx --no-restore --verify-no-changes
Check-Exit
& $dotnet run --project tools/ContentBuilder -c Release --no-build -- $projectRoot
Check-Exit
Write-Output 'Build, tests, formatting and CDN packaging passed. Start-Server.cmd, then run node tools/Verify-Characters.mjs, tools/Verify-Gameplay.mjs, tools/Verify-Tcp.mjs, tools/Verify-World.mjs and tools/Verify-Base.mjs.'
