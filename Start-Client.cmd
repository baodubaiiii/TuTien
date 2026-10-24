@echo off
if not exist "%~dp0UnityClient\Build\TuTienClient.exe" (
    echo Build the Unity demo with tools\Build-Unity.ps1 first.
    pause
    exit /b 1
)
start "" "%~dp0UnityClient\Build\TuTienClient.exe"
