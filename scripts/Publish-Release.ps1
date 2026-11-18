param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.0.0', [switch]$Installer)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$git=(Get-Command git -ErrorAction SilentlyContinue).Source
if(!$git){$git=Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe'}
if(!(Test-Path -LiteralPath $git)){$git='C:\Users\ViniciusAbreudeOlive\AppData\Local\Programs\Git\cmd\git.exe'}
function Git-Run([string[]]$Arguments){& $git -c "safe.directory=$repoRoot" -C $repoRoot @Arguments;if($LASTEXITCODE-ne 0){throw 'Comando Git falhou; publicação interrompida.'}}
$tag="v$Version"
$releaseRoot=Join-Path $repoRoot "artifacts\releases\$tag"
$assets=@((Join-Path $releaseRoot "Eagle-Eyes-$tag-win-x64.zip"),(Join-Path $releaseRoot "Eagle-Eyes-$tag-win-x64.zip.sha256"))
if($Installer){
    $setup=Join-Path $repoRoot "artifacts\installer\$Version\EagleEyes-$Version-win-x64-Setup.exe"
    $assets=@($setup,"$setup.sha256")
    $expectedLine=(Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($setup)
    if(([IO.File]::ReadAllText("$setup.sha256")).Trim() -cne $expectedLine){throw 'SHA256 do instalador divergente.'}
}
foreach($asset in $assets){if(!(Test-Path -LiteralPath $asset)){throw "Prepare o pacote antes: $asset"}}
if(Git-Run -Arguments @('status','--porcelain')){throw 'Commit pendente: a árvore de trabalho precisa estar limpa.'}
$sha=(Git-Run -Arguments @('rev-parse','HEAD')).Trim()
$env:GIT_TERMINAL_PROMPT='0';$env:GCM_INTERACTIVE='Never'
$credential="protocol=https`nhost=github.com`n`n" | & $git credential fill 2>$null
if($LASTEXITCODE-ne 0){throw 'Autentique no GitHub: git credential-manager github login --device --no-ui'}
$passwordLine=$credential | Where-Object {$_ -like 'password=*'} | Select-Object -First 1
if(!$passwordLine){throw 'Credencial GitHub indisponível.'}
$token=$passwordLine.Substring(9)
$headers=@{Authorization="Bearer $token";Accept='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28';'User-Agent'='Eagle-Eyes-Release'}
$api='https://api.github.com/repos/OVinicius1995/Eagle-Eyes'
try{
    $null=Invoke-RestMethod -Uri $api -Headers $headers
    $tags=Git-Run -Arguments @('tag','--list',$tag)
    if($tags){$tagSha=(Git-Run -Arguments @('rev-list','-n','1',$tag)).Trim();if($tagSha-ne $sha){throw 'A tag já aponta para outro commit; não será alterada.'}}
    else{Git-Run -Arguments @('tag','-a',$tag,'-m',"Eagle Eyes $Version")}
    if($Installer){Git-Run -Arguments @('push','origin',"refs/tags/$tag")} else {Git-Run -Arguments @('push','--atomic','-u','origin','main',"refs/tags/$tag")}
    $release=$null
    try{$release=Invoke-RestMethod -Uri "$api/releases/tags/$tag" -Headers $headers}catch{if([int]$_.Exception.Response.StatusCode-ne 404){throw}}
    if(!$release){
        $notes=[IO.File]::ReadAllText((Join-Path $repoRoot "docs\releases\$tag.md"))
        $payload=@{tag_name=$tag;target_commitish=$sha;name="Eagle Eyes $Version";body=$notes;draft=$true;prerelease=$false}|ConvertTo-Json
        $release=Invoke-RestMethod -Method Post -Uri "$api/releases" -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload))
    }
    foreach($asset in $assets){
        $name=[IO.Path]::GetFileName($asset)
        $expected=(Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
        $existing=$release.assets | Where-Object name -EQ $name | Select-Object -First 1
        if($existing){
            if($existing.digest -ne "sha256:$expected"){throw "Anexo existente sem hash correspondente: $name. Não será sobrescrito."}
            continue
        }
        $upload=($release.upload_url -replace '\{.*\}$','')+'?name='+[Uri]::EscapeDataString($name)
        $uploaded=Invoke-RestMethod -Method Post -Uri $upload -Headers $headers -ContentType 'application/octet-stream' -InFile $asset
        if($uploaded.size -ne (Get-Item -LiteralPath $asset).Length){throw 'Tamanho do anexo enviado não confere.'}
        if($uploaded.digest -and $uploaded.digest-ne "sha256:$expected"){throw 'Hash do anexo enviado não confere.'}
    }
    $release=Invoke-RestMethod -Uri "$api/releases/$($release.id)" -Headers $headers
    foreach($asset in $assets){$name=[IO.Path]::GetFileName($asset);if(!($release.assets|Where-Object name -EQ $name)){throw "Anexo ausente: $name"}}
    if($release.draft){$release=Invoke-RestMethod -Method Patch -Uri "$api/releases/$($release.id)" -Headers $headers -ContentType 'application/json' -Body '{"draft":false,"make_latest":"true"}'}
    if($release.draft){throw 'Release ainda está em rascunho.'}
    Write-Host "Release publicado: $($release.html_url)"
    $release.assets | Select-Object name,size,browser_download_url
} finally {$token=$null;$credential=$null;$passwordLine=$null;$headers=$null}