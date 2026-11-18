param([string]$SourcePath=(Join-Path (Split-Path $PSScriptRoot -Parent) 'eagle-eyes-original.png'))
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'BrandAssetTool.cs') -ReferencedAssemblies System.Drawing,System.Core
$assets=Join-Path $repoRoot 'Assets'
$previews=Join-Path $repoRoot '.test-artifacts\brand-preview'
$result=[EagleEyesBrandAssetTool]::Prepare([IO.Path]::GetFullPath($SourcePath),$assets,$previews)
$report=[ordered]@{source=[IO.Path]::GetFileName($SourcePath);sourceSha256=(Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash;sourceWidth=$result[0];sourceHeight=$result[1];foregroundBounds=@{x=$result[2];y=$result[3];width=$result[4];height=$result[5]};opaqueNativePixelsPreserved=$result[6];antialiasedNativePixels=$result[7];outputPng='Assets/eagle-eyes.png';outputSize=1024;targetSymbolWidthFraction=0.85;icoSizes=@(16,20,24,32,48,64,128,256);method='Local white-matte removal with local-color unmix, preserved opaque pixels, alpha-aware resampling; no redraw or recolor.'}
$report | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $assets 'eagle-eyes-source.json')
$report | ConvertTo-Json -Depth 5