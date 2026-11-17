param([string]$Repository = 'C:\Users\ViniciusAbreudeOlive\source\repos\Eagle-Eyes')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class EagleUiInput { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y); [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra); [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra); }'
$artifacts=Join-Path $Repository ('.test-artifacts\ui-flow-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$log=Join-Path $artifacts 'flow.log'
function Log([string]$message){$line=(Get-Date -Format o)+' '+$message; Add-Content -LiteralPath $log -Value $line; Write-Output $line}
$settingsPath=Join-Path $env:LOCALAPPDATA 'EagleEyes\settings.json'
$hadSettings=Test-Path -LiteralPath $settingsPath
$backup=if($hadSettings){[IO.File]::ReadAllBytes($settingsPath)}else{$null}
$app=$null
function Find-Button([string]$name){
 $all=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$app.Id)))
 foreach($item in $all){if($item.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $item.Current.Name -eq $name){return $item}}
 return $null
}
function Invoke-Button([string]$name,[int]$timeout=10){
 $until=(Get-Date).AddSeconds($timeout)
 do{$button=Find-Button $name;if($null-ne $button){$pattern=$button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern);$pattern.Invoke();Log ('Invoked '+$name);return};Start-Sleep -Milliseconds 200}while((Get-Date)-lt $until)
 throw ('Button not found: '+$name)
}
function Hotkey([byte]$key){
 [EagleUiInput]::keybd_event(0x11,0,0,[UIntPtr]::Zero);[EagleUiInput]::keybd_event(0x10,0,0,[UIntPtr]::Zero);[EagleUiInput]::keybd_event($key,0,0,[UIntPtr]::Zero)
 Start-Sleep -Milliseconds 80
 [EagleUiInput]::keybd_event($key,0,2,[UIntPtr]::Zero);[EagleUiInput]::keybd_event(0x10,0,2,[UIntPtr]::Zero);[EagleUiInput]::keybd_event(0x11,0,2,[UIntPtr]::Zero)
}
try{
 if(Get-Process EagleEyes.App -ErrorAction SilentlyContinue){throw 'An Eagle Eyes instance already exists. Close it before this test.'}
 New-Item -ItemType Directory -Force -Path (Split-Path $settingsPath) | Out-Null
 @{OutputDirectory=$artifacts;FfmpegDirectory=(Join-Path $Repository 'components\ffmpeg-master-latest-win64-gpl\bin');MonitorDevice='';Edge='Right';Offset=0.45;Expanded=$true;KeepExpanded=$true;FullMonitor=$false;HideDuringCapture=$true;Countdown=3}|ConvertTo-Json|Set-Content -Encoding UTF8 -LiteralPath $settingsPath
 $exe=Join-Path $Repository 'src\EagleEyes.App\bin\Debug\net11.0-windows\EagleEyes.App.exe'
 $app=Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
 Log ('Launched PID '+$app.Id)
 Invoke-Button 'Gravar vídeo'
 Start-Sleep -Milliseconds 800
 [EagleUiInput]::SetCursorPos(120,120)|Out-Null
 [EagleUiInput]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
 for($i=1;$i-le 20;$i++){[EagleUiInput]::SetCursorPos((120+32*$i),(120+18*$i))|Out-Null;Start-Sleep -Milliseconds 20}
 [EagleUiInput]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
 Log 'Dragged physical region (120,120) to (760,480): 640x360'
 Invoke-Button 'OK'
 Start-Sleep -Seconds 6
 Hotkey 0x50;Log 'Pause1 hotkey'
 Start-Sleep -Seconds 2
 if($null-eq (Find-Button 'Retomar · Ctrl+Shift+P')){throw 'Pause1 did not expose Resume button'}
 Log 'Pause1 Resume button observed'
 Hotkey 0x50;Log 'Resume1 hotkey';Start-Sleep -Seconds 2
 Hotkey 0x50;Log 'Pause2 hotkey';Start-Sleep -Seconds 2
 if($null-eq (Find-Button 'Retomar · Ctrl+Shift+P')){throw 'Pause2 did not expose Resume button'}
 Log 'Pause2 Resume button observed'
 Hotkey 0x50;Log 'Resume2 hotkey';Start-Sleep -Seconds 2
 Hotkey 0x46;Log 'Finalize hotkey'
 $until=(Get-Date).AddSeconds(30)
 do{$output=Get-ChildItem -LiteralPath $artifacts -Filter '*.mp4'|Select-Object -First 1;if($output){break};Start-Sleep -Milliseconds 300}while((Get-Date)-lt $until)
 if(!$output){throw 'No final MP4 after real UI flow'}
 Log ('MP4 '+$output.FullName)
 Invoke-Button 'Fechar Eagle Eyes'
 if(!$app.WaitForExit(5000)){throw 'App did not close after successful recording'}
 $probe=Join-Path $Repository 'components\ffmpeg-master-latest-win64-gpl\bin\ffprobe.exe'
 & $probe -v error -show_streams -show_format -of json $output.FullName | Set-Content -Encoding UTF8 (Join-Path $artifacts 'probe.json')
 if($LASTEXITCODE-ne 0){throw 'Final MP4 probe failed'}
 Log 'PASS actual UI invoke + mouse region + two hotkey pauses + final MP4'
}catch{Log ('FAIL '+$_);throw}
finally{
 if($app-and !$app.HasExited){$app.Kill();$app.WaitForExit()}
 if($hadSettings){[IO.File]::WriteAllBytes($settingsPath,$backup)}elseif(Test-Path -LiteralPath $settingsPath){Remove-Item -LiteralPath $settingsPath}
 Log 'Restored pre-test user settings'
}
