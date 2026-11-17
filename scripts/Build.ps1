param([switch]$Live)
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
    dotnet publish src/EagleEyes.App/EagleEyes.App.csproj -c Release --no-self-contained -o artifacts/eagle-eyes
    if($LASTEXITCODE -ne 0){throw 'Publish falhou.'}
    Write-Host 'Executável: artifacts/eagle-eyes/EagleEyes.App.exe (requer Windows Desktop Runtime .NET11).'
} finally {Pop-Location}
