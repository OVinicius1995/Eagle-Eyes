param([Parameter(Mandatory=$true)][string]$Installer, [string]$BaselineInstaller, [switch]$VerifyEngineReuse)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{A15849CE-44AD-4A1C-AF1F-E97B35A4E377}_is1'
if(Test-Path $key){throw 'Já existe instalação Eagle Eyes registrada. Teste abortado para preservá-la.'}
if(Test-Path (Join-Path ([Environment]::GetFolderPath('Programs')) 'Eagle Eyes\Eagle Eyes.lnk')){throw 'Atalho anterior encontrado; teste abortado para preservá-lo.'}
if(Get-Process EagleEyes.App -ErrorAction SilentlyContinue){throw 'Feche o Eagle Eyes antes do teste isolado.'}
$id=[guid]::NewGuid().ToString('N'); $work=Join-Path $repo ".test-artifacts\installer-$id"; $app=Join-Path ([IO.Path]::GetTempPath()) "Eagle-Eyes-QA-$id"; New-Item -ItemType Directory -Force $work | Out-Null
function SetupVersion([string]$file) {
    if ([IO.Path]::GetFileName($file) -notmatch '^EagleEyes-(\d+\.\d+\.\d+)-win-x64-Setup\.exe$') { throw "Nome de instalador inesperado: $file" }
    return $Matches[1]
}
$version=SetupVersion $Installer
$checks=[Collections.Generic.List[string]]::new()
function Assert($condition,[string]$message){if(!$condition){throw $message}; $checks.Add($message); Write-Host "PASS $message"}
function RunSetup([string]$file){$script:lastSetupLog=Join-Path $work ('setup-'+[guid]::NewGuid().ToString('N')+'.log');$p=Start-Process $file -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$app+'"'),('/GROUP="EagleEyes-Test-'+$id+'"'),('/LOG="'+$script:lastSetupLog+'"')) -Wait -PassThru -WindowStyle Hidden; Assert ($p.ExitCode -eq 0) 'Setup exits successfully'}
$prefs=Join-Path $env:LOCALAPPDATA 'EagleEyes\settings.json'; $before=if(Test-Path $prefs){(Get-FileHash $prefs).Hash}else{''}
$failure=$null
try {
    if($BaselineInstaller){RunSetup $BaselineInstaller; Assert ((Get-Item "$app\EagleEyes.App.exe").VersionInfo.FileVersion -eq ((SetupVersion $BaselineInstaller)+'.0')) 'Baseline version installed'}
    if(!(Test-Path $app)){New-Item -ItemType Directory -Force $app | Out-Null}
    New-Item -ItemType Directory -Force "$app\ffmpeg" | Out-Null
    'external component sentinel' | Set-Content "$app\ffmpeg\user-component.txt"
    'user video sentinel' | Set-Content "$app\prior-video.mp4"
    RunSetup $Installer
    Assert ((Get-Item "$app\EagleEyes.App.exe").VersionInfo.FileVersion -eq ($version+'.0')) 'Requested version installed'
    Assert (Test-Path "$app\coreclr.dll") 'Self-contained runtime present'
    Assert (Test-Path "$app\Assets\eagle-eyes.ico") 'Official icon installed'
    Assert (!(Test-Path "$app\docs\IDENTIDADE-VISUAL.md")) 'Private guide absent'
    Assert (Test-Path "$app\ffmpeg\user-component.txt") 'External component preserved on upgrade'
    Assert (Test-Path "$app\prior-video.mp4") 'Existing user video preserved on upgrade'
    Assert ((Get-ItemProperty $key).DisplayVersion -eq $version) 'Per-user uninstall registration updated'
    $link=Join-Path ([Environment]::GetFolderPath('Programs')) "Eagle Eyes\Eagle Eyes.lnk"
    Assert (Test-Path $link) 'Start menu shortcut created'
    $shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut($link)
    Assert ($shortcut.TargetPath -eq "$app\EagleEyes.App.exe") 'Shortcut points to installed executable'
    Assert ($shortcut.IconLocation -like "*$app\Assets\eagle-eyes.ico*") 'Shortcut uses official icon'
    $report=Join-Path $work 'runtime.json'
    $process=Start-Process "$app\EagleEyes.App.exe" -ArgumentList @('--smoke-test','--ux-test','--branding-report',('"'+$report+'"')) -PassThru -WindowStyle Hidden
    if(!$process.WaitForExit(30000)){throw 'Installed application smoke timed out; no process terminated automatically.'}
    Assert ($process.ExitCode -eq 0) 'Installed application runs and exits normally'
    Assert ((Get-Content $report -Raw | ConvertFrom-Json).trayRegistered) 'Installed application registers tray icon'
    $engine=Join-Path $app 'engine\ffmpeg-9.0.2-essentials_build\bin'
    Assert (Test-Path "$engine\ffmpeg.exe") 'Recorder installed automatically'
    if($VerifyEngineReuse){
        $download='Downloading temporary file from https://github.com/GyanD/codexffmpeg/'
        if(!$BaselineInstaller){RunSetup $Installer}
        Assert (!(Select-String -LiteralPath $lastSetupLog -SimpleMatch $download -Quiet)) 'Upgrade skips the FFmpeg download'
        Assert (Select-String -LiteralPath $lastSetupLog -SimpleMatch 'reutilizando o motor' -Quiet) 'Setup recognizes the verified engine'
        $probe=Join-Path $engine 'ffprobe.exe'
        $recorder=Join-Path $engine 'ffmpeg.exe'
        $probeHash=(Get-FileHash $probe).Hash
        $recorderHash=(Get-FileHash $recorder).Hash
        Remove-Item -LiteralPath $probe
        RunSetup $Installer
        Assert (Select-String -LiteralPath $lastSetupLog -SimpleMatch $download -Quiet) 'Missing FFprobe triggers download'
        Assert ((Get-FileHash $probe).Hash -eq $probeHash) 'Missing FFprobe restored'
        [IO.File]::WriteAllBytes($recorder,[byte[]](0,1,2))
        RunSetup $Installer
        Assert (Select-String -LiteralPath $lastSetupLog -SimpleMatch $download -Quiet) 'Damaged FFmpeg triggers download'
        Assert ((Get-FileHash $recorder).Hash -eq $recorderHash) 'Damaged FFmpeg repaired'
    }
    $capture=Join-Path $work 'recording'
    $proof=Join-Path $work 'proof.txt'
    $process=Start-Process "$app\EagleEyes.App.exe" -ArgumentList @('--proof-recording','--output',('"'+$capture+'"'),'--report',('"'+$proof+'"')) -WindowStyle Hidden -PassThru
    if(!$process.WaitForExit(60000)){throw 'Recording proof timed out'}
    $video=Get-Content $proof -TotalCount 1
    Assert (Test-Path -LiteralPath $video -PathType Leaf) 'Installed app saved a recording after two pauses'
    Assert (@(Get-ChildItem $capture -Force).Count -eq 1) 'Only final video remains in recording folder'
    $media=(& "$engine\ffprobe.exe" -v error -show_streams -show_format -of json $video | Out-String | ConvertFrom-Json)
    Assert ($media.streams.Count -eq 1 -and $media.streams[0].codec_name -eq 'h264' -and [double]::Parse($media.format.duration,[cultureinfo]::InvariantCulture) -gt 0) 'MP4 contains H.264 video only'
    $mutex=[Threading.Mutex]::new($true,'Local\EagleEyes.Desktop')
    try {
        $blocked=Start-Process $Installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$app+'"')) -Wait -PassThru -WindowStyle Hidden
        Assert ($blocked.ExitCode -ne 0) 'Setup refuses running-app mutex'
        $blocked=Start-Process "$app\unins000.exe" -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru -WindowStyle Hidden
        Assert (Test-Path "$app\EagleEyes.App.exe") 'Uninstall refuses running-app mutex'
    } finally {$mutex.ReleaseMutex(); $mutex.Dispose()}
    $remove=Start-Process "$app\unins000.exe" -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+$work+'\uninstall.log"')) -Wait -PassThru -WindowStyle Hidden
    Assert ($remove.ExitCode -eq 0) 'Uninstaller exits successfully'
    Assert (!(Test-Path "$app\EagleEyes.App.exe")) 'Program removed'
    Assert (!(Test-Path $key)) 'Uninstall registration removed'
    Assert (!(Test-Path $link)) 'Shortcut removed'
    Assert (Test-Path "$app\ffmpeg\user-component.txt") 'External component preserved after uninstall'
    Assert (Test-Path "$app\prior-video.mp4") 'User video preserved after uninstall'
    $after=if(Test-Path $prefs){(Get-FileHash $prefs).Hash}else{''}
    Assert ($before -eq $after) 'User settings unchanged'
} catch {$failure=$_.Exception.ToString(); throw}
finally {@{Checks=$checks;Passed=$checks.Count;Failure=$failure;Baseline=$BaselineInstaller;Installer=$Installer} | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 "$work\result.json"; Write-Host "EVIDENCE $work"}
