@echo off
cd /d "%~dp0"
if exist "artifacts\eagle-eyes\EagleEyes.App.exe" (
  start "" "artifacts\eagle-eyes\EagleEyes.App.exe"
) else (
  dotnet run --project "src\EagleEyes.App\EagleEyes.App.csproj" -c Release
)
