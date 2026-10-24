param([switch]$SkipBuild)
& (Join-Path $PSScriptRoot 'Start-Server.ps1') -SkipBuild:$SkipBuild
