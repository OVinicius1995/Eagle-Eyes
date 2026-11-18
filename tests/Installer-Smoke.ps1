param([Parameter(Mandatory=$true)][string]$Installer, [string]$BaselineInstaller)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{A15849CE-44AD-4A1C-AF1F-E97B35A4E377}_is1'
if(Test-Path $key){throw 'Já existe instalação Eagle Eyes registrada. Teste abortado para preservá-la.'}
if(Test-Path (Join-Path ([Environment]::GetFolderPath('Programs')) 'Eagle Eyes\Eagle Eyes.lnk')){throw 'Atalho anterior encontrado; teste abortado para preservá-lo.'}
if(Get-Process EagleEyes.App -ErrorAction SilentlyContinue){throw 'Feche o Eagle Eyes antes do teste isolado.'}
$id=[guid]::NewGuid().ToString('N'); $work=Join-Path $repo ".test-artifacts\installer-$id"; $app=Join-Path $work 'installed'; New-Item -ItemType Directory -Force $work | Out-Null
$checks=[Collections.Generic.List[string]]::new()
function Assert($condition,[string]$message){if(!$condition){throw $message}; $checks.Add($message); Write-Host "PASS $message"}
function RunSetup([string]$file){$p=Start-Process $file -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$app+'"'),('/GROUP="EagleEyes-Test-'+$id+'"'),('/LOG="'+$work+'\setup-'+[guid]::NewGuid().ToString('N')+'.log"')) -Wait -PassThru -WindowStyle Hidden; Assert ($p.ExitCode -eq 0) 'Setup exits successfully'}
$prefs=Join-Path $env:LOCALAPPDATA 'EagleEyes\settings.json'; $before=if(Test-Path $prefs){(Get-FileHash $prefs).Hash}else{''}
$failure=$null
try {
    if($BaselineInstaller){RunSetup $BaselineInstaller; Assert ((Get-Item "$app\EagleEyes.App.exe").VersionInfo.FileVersion -eq '1.1.0.0') 'Baseline 1.1.0 installed'}
    if(!(Test-Path $app)){New-Item -ItemType Directory -Force $app | Out-Null}
    New-Item -ItemType Directory -Force "$app\ffmpeg" | Out-Null
    'external component sentinel' | Set-Content "$app\ffmpeg\user-component.txt"
    'user video sentinel' | Set-Content "$app\prior-video.mp4"
    RunSetup $Installer
    Assert ((Get-Item "$app\EagleEyes.App.exe").VersionInfo.FileVersion -eq '1.2.0.0') 'Installed 1.2.0 executable'
    Assert (Test-Path "$app\coreclr.dll") 'Self-contained runtime present'
    Assert (Test-Path "$app\Assets\eagle-eyes.ico") 'Official icon installed'
    Assert (!(Test-Path "$app\docs\IDENTIDADE-VISUAL.md")) 'Private guide absent'
    Assert (Test-Path "$app\ffmpeg\user-component.txt") 'External component preserved on upgrade'
    Assert (Test-Path "$app\prior-video.mp4") 'Existing user video preserved on upgrade'
    Assert ((Get-ItemProperty $key).DisplayVersion -eq '1.2.0') 'Per-user uninstall registration updated'
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
