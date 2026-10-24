param([switch]$PrefabsOnly, [switch]$Verify)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$editor = if ($env:UNITY_EDITOR) { $env:UNITY_EDITOR } else { 'E:/Unity/UnityEditor/2022.3.35f1/Editor/Unity.exe' }
if (-not (Test-Path -LiteralPath $editor)) { throw 'Set UNITY_EDITOR to the Unity 2022.3.35f1 executable.' }
$unityProject = Join-Path $projectRoot 'UnityClient'
$artifactPath = Join-Path $projectRoot 'artifacts/unity'
New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
$method = if ($PrefabsOnly) { 'TuTien.Client.Editor.DemoBuilder.Build' } else { 'TuTien.Client.Editor.DemoBuilder.BuildPlayer' }
$buildLog = Join-Path $artifactPath 'build.log'
$arguments = '-batchmode -nographics -quit -projectPath "' + $unityProject + '" -executeMethod ' + $method + ' -logFile "' + $buildLog + '"'
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity build failed. Read $buildLog" }
$licensePath = Join-Path $unityProject 'ThirdParty/Freeman-OFL.txt'
if (-not $PrefabsOnly -and (Test-Path -LiteralPath $licensePath)) {
    $notices = Join-Path $unityProject 'Build/ThirdParty'
    New-Item -ItemType Directory -Path $notices -Force | Out-Null
    Copy-Item -LiteralPath $licensePath -Destination $notices
}
Write-Output 'Unity scenes, prefabs and CDN bundle built.'
if ($Verify) {
    if ($PrefabsOnly) { throw 'Verification needs a player build.' }
    $health = Invoke-RestMethod -Uri 'http://127.0.0.1:5080/health'
    if (-not $health.tcpReady) { throw 'Start the local game server before Unity verification.' }
    $qaPath = Join-Path $artifactPath 'qa'
    New-Item -ItemType Directory -Path $qaPath -Force | Out-Null
    $playerLog = Join-Path $artifactPath 'player.log'
    $smokeStarted = [DateTime]::UtcNow
    $arguments = '-batchmode -tutienSmoke -smokeOutput "' + $qaPath + '" -logFile "' + $playerLog + '" -screen-width 1280 -screen-height 720'
    $process = Start-Process -FilePath (Join-Path $unityProject 'Build/TuTienClient.exe') -ArgumentList $arguments -WorkingDirectory (Join-Path $unityProject 'Build') -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity smoke failed. Read $qaPath/result.json and $playerLog" }
    $result = Get-Content -LiteralPath (Join-Path $qaPath 'result.json') -Raw | ConvertFrom-Json
    if ((Get-Item -LiteralPath (Join-Path $qaPath 'result.json')).LastWriteTimeUtc -lt $smokeStarted) { throw 'Unity smoke did not produce a fresh result.' }
    if (-not $result.success) { throw 'Unity verification did not pass.' }
    if (Select-String -LiteralPath $playerLog -Pattern 'NullReferenceException|MissingReferenceException|Unhandled Exception' -Quiet) {
        throw 'Unity runtime exception found in player.log.'
    }
    Write-Output 'Unity native gameplay and visual QA passed.'
}
