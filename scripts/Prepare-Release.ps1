param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.0.0')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$releaseRoot=Join-Path $repoRoot "artifacts\releases\v$Version"
$staging=Join-Path $releaseRoot ('package-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $staging | Out-Null
$published=Join-Path $releaseRoot ('build-'+[Guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $published
Get-ChildItem -LiteralPath $published -File | Where-Object Extension -NE '.pdb' | Copy-Item -Destination $staging
New-Item -ItemType Directory -Force (Join-Path $staging 'docs') | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\DEPENDENCIES.md') -Destination (Join-Path $staging 'docs')
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\releases\v$Version.md") -Destination (Join-Path $staging 'NOTAS-DA-VERSAO.md')
$instructions=@"
Eagle Eyes $Version - Windows x64

1. Extraia TODO o ZIP. Não execute o aplicativo dentro do arquivo compactado.
2. Instale .NET Desktop Runtime 11 para Windows x64, se necessário:
   https://dotnet.microsoft.com/download/dotnet/11.0
3. Obtenha uma distribuição Windows de FFmpeg com gdigrab e libx264.
   FFmpeg e FFprobe são dependências externas e não estão incluídos.
   Coloque ffmpeg.exe e ffprobe.exe em uma subpasta chamada ffmpeg ao lado
   de EagleEyes.App.exe, ou indique a pasta nas configurações do aplicativo.
   Preserve licenças/avisos da distribuição obtida; veja docs/DEPENDENCIES.md.
4. Abra EagleEyes.App.exe.

Clique na engrenagem para configurar a saída e o modo região/monitor inteiro.
Clique no vídeo e arraste a região. Aguarde a contagem regressiva.
A barra fica oculta enquanto grava para não aparecer no vídeo:
Ctrl+Shift+P: pausar/retomar (barra reaparece durante a pausa).
Ctrl+Shift+F: finalizar em um único MP4 sem áudio.

Pastas .eagle-eyes-session-* guardam temporários inclusive após sucesso.
Remova-as manualmente somente após conferir o MP4.

Código, instruções e problemas:
https://github.com/OVinicius1995/Eagle-Eyes
"@
[IO.File]::WriteAllText((Join-Path $staging 'LEIA-ME.txt'),$instructions,[Text.UTF8Encoding]::new($true))
$zip=Join-Path $releaseRoot "Eagle-Eyes-v$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -Force
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zip+'.sha256'),($hash+'  '+[IO.Path]::GetFileName($zip)+[Environment]::NewLine),[Text.Encoding]::ASCII)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries=@($archive.Entries | ForEach-Object FullName)
    foreach($required in @('EagleEyes.App.exe','EagleEyes.App.dll','EagleEyes.Core.dll','EagleEyes.App.runtimeconfig.json','LEIA-ME.txt')){
        if($entries -notcontains $required){throw "Pacote incompleto: $required"}
    }
    if($entries | Where-Object {$_ -match '(?i)(ffmpeg\.exe|ffprobe\.exe|\.mp4$|\.pdb$)'}){throw 'Conteúdo externo/temporário inesperado no pacote.'}
} finally {$archive.Dispose()}
Write-Host "ZIP: $zip"
Write-Host "SHA256: $hash"