param([string]$CompilerPath)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
[xml]$project=Get-Content "$repo\src\EagleEyes.App\EagleEyes.App.csproj"
$version=[string]$project.Project.PropertyGroup.Version
if (!$CompilerPath) { $CompilerPath=(Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
if (!$CompilerPath -or !(Test-Path -LiteralPath $CompilerPath)) { throw 'Instale Inno Setup e informe -CompilerPath com o caminho de ISCC.exe.' }
$output=Join-Path $repo "artifacts\installer\$version"
$publish=Join-Path $output ('app-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $publish | Out-Null
& dotnet publish "$repo\src\EagleEyes.App\EagleEyes.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $publish
if($LASTEXITCODE -ne 0) { throw 'Publicação self-contained falhou.' }
# Runtime notices are not copied automatically by dotnet publish.
$deps=Get-Content "$publish\EagleEyes.App.deps.json" -Raw | ConvertFrom-Json
$assets=Get-Content "$repo\src\EagleEyes.App\obj\project.assets.json" -Raw | ConvertFrom-Json
foreach($runtime in $deps.libraries.PSObject.Properties.Name | Where-Object {$_ -like 'runtimepack.*'}) {
    $package=$runtime.Substring('runtimepack.'.Length).ToLowerInvariant()
    $source=$null
    foreach($cache in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate=Join-Path $cache $package
        if(Test-Path -LiteralPath $candidate){$source=$candidate;break}
    }
    if(!$source){throw "Licença do runtime não localizada: $runtime"}
    $noticeDir=Join-Path $publish ('licenses\'+$package.Replace('/','-'))
    New-Item -ItemType Directory -Force $noticeDir | Out-Null
    $notices=@(Get-ChildItem $source -File | Where-Object {$_.Name -match '^LICENSE(\.TXT)?$|^THIRD-PARTY-NOTICES(\.TXT)?$'})
    if(!$notices){throw "Avisos do runtime não encontrados: $runtime"}
    $notices | Copy-Item -Destination $noticeDir
}
Copy-Item "$repo\docs\DEPENDENCIES.md" "$publish\DEPENDENCIES.md"
Copy-Item (Join-Path (Split-Path $CompilerPath -Parent) 'license.txt') "$publish\INNO-LICENSE.txt"
$readme = 'O instalador prepara o FFmpeg automaticamente. Para gravar, abra o Eagle Eyes e escolha a área.'
$readme | Set-Content -Encoding UTF8 "$publish\LEIA-ME.txt"
if(Get-ChildItem $publish -Recurse -File | Where-Object { $_.Name -match '^(ffmpeg|ffprobe)\.exe$|IDENTIDADE-VISUAL|\.mp4$|\.pdb$' }) { throw 'Arquivo proibido no pacote.' }
$previousVersion=$env:EAGLE_EYES_VERSION; $previousPublish=$env:EAGLE_EYES_PUBLISH; $previousOutput=$env:EAGLE_EYES_INSTALLER_OUTPUT
try {
    $env:EAGLE_EYES_VERSION=$version; $env:EAGLE_EYES_PUBLISH=$publish; $env:EAGLE_EYES_INSTALLER_OUTPUT=$output
    & $CompilerPath /Q "$repo\installer\EagleEyes.iss"
    if($LASTEXITCODE -ne 0) { throw 'Compilação do instalador falhou.' }
} finally { $env:EAGLE_EYES_VERSION=$previousVersion; $env:EAGLE_EYES_PUBLISH=$previousPublish; $env:EAGLE_EYES_INSTALLER_OUTPUT=$previousOutput }
$setup=Join-Path $output "EagleEyes-$version-win-x64-Setup.exe"
$hash=(Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($setup))" | Set-Content -Encoding ASCII "$setup.sha256"
Write-Host "Instalador: $setup"
