param([switch]$Live, [string]$OutputDirectory="artifacts/eagle-eyes")
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    dotnet build EagleEyes.slnx -c Release
    if($LASTEXITCODE -ne 0){throw 'Build falhou.'}
    dotnet run --project tests/EagleEyes.Tests -c Release --no-build
    if($LASTEXITCODE -ne 0){throw 'Testes falharam.'}
    if($Live){
        dotnet run --project tests/EagleEyes.Tests -c Release --no-build -- --live
        if($LASTEXITCODE -ne 0){throw 'Prova real falhou.'}
    }
    dotnet publish src/EagleEyes.App/EagleEyes.App.csproj -c Release --no-self-contained -o $OutputDirectory
    if($LASTEXITCODE -ne 0){throw 'Publish falhou.'}
    Write-Host "Executável: $OutputDirectory/EagleEyes.App.exe (requer Windows Desktop Runtime .NET 11)."
} finally {Pop-Location}
