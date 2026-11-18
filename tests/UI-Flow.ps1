param(
 [string]$Repository='C:\Users\ViniciusAbreudeOlive\source\repos\Eagle-Eyes',
 [ValidateSet('Functional','Exclusion','ExclusionControl','Drag','All')][string]$Scenario='All'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing,System.Windows.Forms
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class EagleUiInput { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y); [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra); [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra); [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd); [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }'
[EagleUiInput]::SetProcessDPIAware()|Out-Null
$artifacts=Join-Path $Repository ('.test-artifacts\ux11-ui-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $artifacts|Out-Null
$log=Join-Path $artifacts 'flow.log'
function Log([string]$message){$line=(Get-Date -Format o)+' '+$message;Add-Content -LiteralPath $log -Value $line;Write-Host $line}
function Assert([bool]$condition,[string]$message){if(!$condition){throw $message};Log ('PASS '+$message)}
$settingsPath=Join-Path $env:LOCALAPPDATA 'EagleEyes\settings.json'
$hadSettings=Test-Path -LiteralPath $settingsPath
$backup=if($hadSettings){[IO.File]::ReadAllBytes($settingsPath)}else{$null}
$settingsTouched=$false;$app=$null;$background=$null;$outputDirectory=''
$ffmpeg=Join-Path $Repository 'components\ffmpeg-master-latest-win64-gpl\bin\ffmpeg.exe'
$ffprobe=Join-Path $Repository 'components\ffmpeg-master-latest-win64-gpl\bin\ffprobe.exe'
$monitor=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
function Elements{
 if(!$app-or $app.HasExited){return @()}
 $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$app.Id)
 $windows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,$condition)
 $result=[Collections.Generic.List[object]]::new()
 foreach($window in $windows){$result.Add($window);foreach($child in $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)){$result.Add($child)}}
 return $result.ToArray()
}
function Find-Element([string]$name='', [string]$id='', [string]$kind=''){
 foreach($element in (Elements)){
  if($name-and $element.Current.Name-ne $name){continue};if($id-and $element.Current.AutomationId-ne $id){continue}
  if($kind-and $element.Current.ControlType.ProgrammaticName-ne ('ControlType.'+$kind)){continue}
  if($kind-eq 'Button'-and !$element.Current.IsEnabled){continue}
  return $element
 };return $null
}
function Wait-Element([string]$name='', [string]$id='', [string]$kind='', [int]$seconds=12){
 $until=(Get-Date).AddSeconds($seconds)
 do{$element=Find-Element -name $name -id $id -kind $kind;if($null-ne $element){return $element};Start-Sleep -Milliseconds 100}while((Get-Date)-lt $until)
 $snapshot=@(Elements|ForEach-Object {[pscustomobject]@{Name=$_.Current.Name;Id=$_.Current.AutomationId;Type=$_.Current.ControlType.ProgrammaticName}})
 $snapshot|ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $outputDirectory 'failed-uia.json')
 throw ('Element not found: '+$name+' '+$id+' '+$kind)
}
function Invoke-Button([string]$name){$button=Wait-Element -name $name -kind Button;$pattern=$button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern);$pattern.Invoke();Log ('Invoked '+$name)}
function Click-Button([string]$name){
 $button=Wait-Element -name $name -kind Button;$rect=$button.Current.BoundingRectangle;$x=[int]($rect.Left+$rect.Width/2);$y=[int]($rect.Top+$rect.Height/2)
 [EagleUiInput]::SetCursorPos($x,$y)|Out-Null;[EagleUiInput]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[EagleUiInput]::SetCursorPos(($x+2),($y+1))|Out-Null;[EagleUiInput]::mouse_event(4,0,0,0,[UIntPtr]::Zero);Log ('Mouse click with2px jitter '+$name)
}
function Key([byte]$key){[EagleUiInput]::keybd_event($key,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 40;[EagleUiInput]::keybd_event($key,0,2,[UIntPtr]::Zero)}
function Hotkey([byte]$key){[EagleUiInput]::keybd_event(0x11,0,0,[UIntPtr]::Zero);[EagleUiInput]::keybd_event(0x10,0,0,[UIntPtr]::Zero);Key $key;[EagleUiInput]::keybd_event(0x10,0,2,[UIntPtr]::Zero);[EagleUiInput]::keybd_event(0x11,0,2,[UIntPtr]::Zero)}
function Focus-Window([string]$name){$window=Wait-Element -name $name -kind Window;[EagleUiInput]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle)|Out-Null;Start-Sleep -Milliseconds 100}
function Select-Area([bool]$wholeMonitor){
 Invoke-Button 'Gravar vídeo';Focus-Window 'Eagle Eyes · Selecionar área'
 if($wholeMonitor){[EagleUiInput]::SetCursorPos(($monitor.Left+$monitor.Width/2),($monitor.Top+$monitor.Height/2))|Out-Null;Key 0x20;Log 'Space selected monitor under pointer'}
 else{
  $x=$monitor.Left+120;$y=$monitor.Top+120
  [EagleUiInput]::SetCursorPos($x,$y)|Out-Null;[EagleUiInput]::mouse_event(2,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 150
  for($i=1;$i-le 20;$i++){[EagleUiInput]::SetCursorPos(($x+32*$i),($y+18*$i))|Out-Null;Start-Sleep -Milliseconds 15}
  [EagleUiInput]::mouse_event(4,0,0,0,[UIntPtr]::Zero);Log 'Mouse selected physical region640x360'
 }
 $null=Wait-Element -name Gravar -kind Button
 $script:segmentBaseline=@(Get-ChildItem -LiteralPath $outputDirectory -Filter "segment-*.mp4" -Recurse -ErrorAction SilentlyContinue).Count
}
function Observe-Countdown{
 $seen=[Collections.Generic.HashSet[string]]::new();$until=(Get-Date).AddSeconds(6)
 do{
  $value=Find-Element -id CountdownValue
  if($value){$digit=$value.Current.Name;if($digit-in @('3','2','1')){if($seen.Add($digit)){Log ('Countdown '+$digit)};Assert (@(Get-ChildItem -LiteralPath $outputDirectory -Filter 'segment-*.mp4' -Recurse -ErrorAction SilentlyContinue).Count-eq $script:segmentBaseline) ('No recorded segment during countdown '+$digit)}}
  if(Find-Element -name 'Pausar gravação' -kind Button){break}
  Start-Sleep -Milliseconds 80
 }while((Get-Date)-lt $until)
 Assert ($seen.Contains('3')-and $seen.Contains('2')-and $seen.Contains('1')) 'Countdown exposed3,2,1 before capture'
 $null=Wait-Element -name 'Pausar gravação' -kind Button
}
function Current-Text([string]$id){$element=Wait-Element -id $id;return $element.Current.Name}
function Save-Recording{
 Invoke-Button 'Parar e salvar'
 $null=Wait-Element -name 'Seu vídeo foi salvo!'
 $null=Wait-Element -name 'Abrir pasta' -kind Button
 Invoke-Button 'Concluir'
 $until=(Get-Date).AddSeconds(15)
 do{$output=Get-ChildItem -LiteralPath $outputDirectory -Filter '*.mp4'|Select-Object -First 1;if($output){break};Start-Sleep -Milliseconds 100}while((Get-Date)-lt $until)
 Assert ($null-ne $output) 'FinalMP4 exists'
 & $ffprobe -v error -show_streams -show_format -of json $output.FullName|Set-Content -Encoding UTF8 (Join-Path $outputDirectory 'probe.json');if($LASTEXITCODE-ne 0){throw 'Probe failed'}
 $probe=Get-Content -Raw (Join-Path $outputDirectory 'probe.json')|ConvertFrom-Json
 Assert (@($probe.streams|Where-Object codec_type -eq video).Count-eq 1) 'One video stream'
 Assert (@($probe.streams|Where-Object codec_type -eq audio).Count-eq 0) 'No audio stream'
 Assert ($probe.streams[0].r_frame_rate-eq '15/1') '15fps'
 & $ffmpeg -v error -i $output.FullName -f null NUL;if($LASTEXITCODE-ne 0){throw 'Full decode failed'}
 Log ('MP4 '+$output.FullName);return $output.FullName
}
function Ancestor-Window($element){while($element-and $element.Current.ControlType-ne [System.Windows.Automation.ControlType]::Window){$element=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($element)};return $element}
function Drag-Point([int]$x,[int]$y,[int]$dx,[int]$dy){
 [EagleUiInput]::SetCursorPos($x,$y)|Out-Null;[EagleUiInput]::mouse_event(2,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 150
 for($i=1;$i-le 20;$i++){[EagleUiInput]::SetCursorPos(($x+[int]($dx*$i/20)),($y+[int]($dy*$i/20)))|Out-Null;Start-Sleep -Milliseconds 25}
 [EagleUiInput]::mouse_event(4,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 350
}
function Assert-Moved($before,$after,[string]$name){
 Assert ([Math]::Abs($after.Left-$before.Left)-gt 30-or [Math]::Abs($after.Top-$before.Top)-gt 30) ($name+' moved after drag threshold')
 $work=[Windows.Forms.Screen]::PrimaryScreen.WorkingArea
 Assert ($after.Left-ge $work.Left-and $after.Top-ge $work.Top-and $after.Right-le $work.Right-and $after.Bottom-le $work.Bottom) ($name+' remains inside workarea')
}
function Capture-Screen([string]$path){
 & $ffmpeg -v error -nostdin -n -f gdigrab -framerate 15 -draw_mouse 0 -offset_x $monitor.Left -offset_y $monitor.Top -video_size ($monitor.Width.ToString()+'x'+$monitor.Height) -i desktop -frames:v 1 -update 1 $path
 if($LASTEXITCODE-ne 0){throw 'gdigrab capture failed'}
}
function Magenta-Ratio([string]$path,[System.Drawing.Rectangle]$crop){
 $bitmap=[Drawing.Bitmap]::new($path);try{$count=0;$total=0;for($y=$crop.Top;$y-lt $crop.Bottom;$y+=2){for($x=$crop.Left;$x-lt $crop.Right;$x+=2){$pixel=$bitmap.GetPixel($x,$y);if($pixel.R-gt 220-and $pixel.G-lt 45-and $pixel.B-gt 220){$count++};$total++}};return $count/[double]$total}finally{$bitmap.Dispose()}
}
function Close-TestApp{
 if($app-and !$app.HasExited){Invoke-Button 'Fechar Eagle Eyes';if(!$app.WaitForExit(5000)){throw 'Test app did not close'}}
}
try{
 if(Get-Process EagleEyes.App -ErrorAction SilentlyContinue){throw 'BLOCKED: Eagle Eyes already running. Close the user instance before real UI automation; it will not be terminated.'}
 New-Item -ItemType Directory -Force -Path (Split-Path $settingsPath)|Out-Null
 $ready=Join-Path $artifacts 'background.json'
 $helper=Join-Path $Repository 'tests\EagleEyes.AffinityProof\bin\Debug\net11.0-windows\EagleEyes.AffinityProof.exe'
 $bounds=$monitor.Left.ToString()+','+$monitor.Top+','+$monitor.Width+','+$monitor.Height
 $background=Start-Process -FilePath $helper -ArgumentList @('--background','--bounds',$bounds,'--ready',$ready) -WindowStyle Hidden -PassThru
 $until=(Get-Date).AddSeconds(10);while(!(Test-Path -LiteralPath $ready)-and (Get-Date)-lt $until){Start-Sleep -Milliseconds 100};Assert (Test-Path -LiteralPath $ready) 'Magenta background ready'
 $scenarios=if($Scenario-eq 'All'){@('Functional','ExclusionControl','Exclusion','Drag')}else{@($Scenario)}
 foreach($currentScenario in $scenarios){
  $outputDirectory=Join-Path $artifacts $currentScenario;New-Item -ItemType Directory -Force -Path $outputDirectory|Out-Null
  @{OutputDirectory=$outputDirectory;FfmpegDirectory=(Join-Path $Repository 'components\ffmpeg-master-latest-win64-gpl\bin');MonitorDevice='';Edge='Right';Offset=0.45;Expanded=$true;KeepExpanded=$true;FullMonitor=$false;HideDuringCapture=$false;Countdown=3}|ConvertTo-Json|Set-Content -Encoding UTF8 -LiteralPath $settingsPath
  $settingsTouched=$true;$arguments=if($currentScenario-eq 'ExclusionControl'){'--ux-test --disable-capture-exclusion'}else{'--ux-test'}
  $exe=Join-Path $Repository 'src\EagleEyes.App\bin\Debug\net11.0-windows\EagleEyes.App.exe';$app=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru;Log ('Scenario '+$currentScenario+' PID '+$app.Id)
  if($currentScenario-eq 'Functional'){
   Invoke-Button 'Gravar vídeo';Focus-Window 'Eagle Eyes · Selecionar área';Key 0x1B;$null=Wait-Element -name 'Gravar vídeo' -kind Button;Log 'PASS Esc initial selection returns idle'
   Select-Area $true;Start-Sleep -Milliseconds 400;Assert ($null-ne (Find-Element -name Gravar -kind Button)) 'Space does not leak into preparationStart'
   Invoke-Button 'Selecionar novamente';Focus-Window 'Eagle Eyes · Selecionar área';Key 0x1B;$null=Wait-Element -name 'Gravar vídeo' -kind Button;Log 'PASS Esc reselection returns principal'
   Select-Area $false;Invoke-Button 'Cancelar';$null=Wait-Element -name 'Gravar vídeo' -kind Button
   Select-Area $false
   Click-Button 'Gravar';Focus-Window 'Eagle Eyes · Contagem';Key 0x1B;$null=Wait-Element -name Gravar -kind Button;Log 'PASS Esc countdown returns preparation'
   Assert (@(Get-ChildItem -LiteralPath $outputDirectory -Filter 'segment-*.mp4' -Recurse).Count-eq 0) 'Cancelled countdown recorded no segments'
   Invoke-Button 'Gravar';Observe-Countdown;Start-Sleep -Seconds 2
   Invoke-Button 'Pausar gravação';$null=Wait-Element -name 'Retomar gravação' -kind Button;Assert ((Current-Text RecordingStatus)-eq 'Pausado') 'Textual paused status'
   $duration=Current-Text RecordingDuration;Start-Sleep -Milliseconds 1200;Assert ((Current-Text RecordingDuration)-eq $duration) 'Paused duration remains frozen'
   Invoke-Button 'Descartar gravação';$null=Wait-Element -name 'Descartar gravação?' -kind Window;Invoke-Button 'Continuar gravação';$null=Wait-Element -name 'Retomar gravação' -kind Button;Log 'PASS Cancel discard restores paused state'
   Invoke-Button 'Retomar gravação';Start-Sleep -Seconds 2;Hotkey 0x50;$null=Wait-Element -name 'Retomar gravação' -kind Button;Hotkey 0x50;$null=Wait-Element -name 'Pausar gravação' -kind Button;Start-Sleep -Seconds 2
   $output=Save-Recording
   $probe=Get-Content -Raw (Join-Path $outputDirectory 'probe.json')|ConvertFrom-Json;Assert ($probe.streams[0].width-eq 640-and $probe.streams[0].height-eq 360) 'Mouse region dimensions preserved'
   $preservedBytes=[IO.File]::ReadAllBytes($output)
   Select-Area $false;Invoke-Button 'Gravar';Observe-Countdown;Start-Sleep -Seconds 1
   Invoke-Button 'Descartar gravação';$null=Wait-Element -name 'Descartar gravação?' -kind Window;Invoke-Button 'Continuar gravação';$null=Wait-Element -name 'Pausar gravação' -kind Button;Log 'PASS Cancel discard restores recording state'
   Invoke-Button 'Descartar gravação';$dialog=Wait-Element -name 'Descartar gravação?' -kind Window;$confirm=$dialog.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Descartar gravação'),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)));$confirm.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
   $null=Wait-Element -name 'Gravar vídeo' -kind Button;Assert (@(Get-ChildItem -LiteralPath $outputDirectory -Filter '*.mp4').Count-eq 1) 'Discard generated no second finalMP4';Assert (([Convert]::ToBase64String([IO.File]::ReadAllBytes($output))-eq [Convert]::ToBase64String($preservedBytes))) 'Discard preserved earlier video'
  }elseif($currentScenario-eq 'Drag'){
   $idle=Ancestor-Window (Wait-Element -name 'Gravar vídeo' -kind Button);$before=$idle.Current.BoundingRectangle
   Drag-Point ([int]($before.Right-22)) ([int]($before.Bottom-14)) -100 80
   $idle=Ancestor-Window (Wait-Element -name 'Gravar vídeo' -kind Button);$after=$idle.Current.BoundingRectangle;Assert-Moved $before $after 'Idle bar background'
   Select-Area $false
   $heading=Wait-Element -name 'Tudo pronto! Clique em Gravar para começar.';$prep=Ancestor-Window $heading;$before=$prep.Current.BoundingRectangle;$point=$heading.Current.BoundingRectangle
   Drag-Point ([int]($point.Left+$point.Width/2)) ([int]($point.Top+$point.Height/2)) 110 65
   $prep=Ancestor-Window (Wait-Element -name Gravar -kind Button);$after=$prep.Current.BoundingRectangle;Assert-Moved $before $after 'Preparation heading'
   Click-Button 'Gravar';Observe-Countdown;Start-Sleep -Seconds 2
   $duration=Wait-Element -id RecordingDuration;$bar=Ancestor-Window $duration;$before=$bar.Current.BoundingRectangle;$point=$duration.Current.BoundingRectangle
   Drag-Point ([int]($point.Left+$point.Width/2)) ([int]($point.Top+$point.Height/2)) 110 65
   $bar=Ancestor-Window (Wait-Element -id RecordingDuration);$after=$bar.Current.BoundingRectangle;Assert-Moved $before $after 'Recording chronometer'
   $before=$after;Click-Button 'Pausar gravação';$null=Wait-Element -name 'Retomar gravação' -kind Button;$bar=Ancestor-Window (Wait-Element -id RecordingDuration);$after=$bar.Current.BoundingRectangle
   Assert ([Math]::Abs($after.Left-$before.Left)-lt 3-and [Math]::Abs($after.Top-$before.Top)-lt 3) 'Pause button click with2px jitter never drags window'
   Log 'Saving while explicitly paused';$output=Save-Recording
  }else{
   Select-Area $true;Invoke-Button 'Gravar';Observe-Countdown;Start-Sleep -Seconds 2
   $pause=Wait-Element -name 'Pausar gravação' -kind Button
   $recordingWindow=$pause;while($recordingWindow-and $recordingWindow.Current.ControlType-ne [System.Windows.Automation.ControlType]::Window){$recordingWindow=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($recordingWindow)}
   if(!$recordingWindow){throw 'Recording window not found'}
   $rect=$recordingWindow.Current.BoundingRectangle;Assert (!$recordingWindow.Current.IsOffscreen) 'Actual recordingbar visible during recording'
   $crop=[Drawing.Rectangle]::new([int]($rect.Left-$monitor.Left+12),[int]($rect.Top-$monitor.Top+12),[int]($rect.Width-24),[int]($rect.Height-24))
   $screen=Join-Path $outputDirectory 'actualbar-gdigrab.png';Capture-Screen $screen;$ratio=Magenta-Ratio $screen $crop
   $output=Save-Recording;$frame=Join-Path $outputDirectory 'firstframe.png';& $ffmpeg -v error -nostdin -n -i $output -frames:v 1 -update 1 $frame;if($LASTEXITCODE-ne 0){throw 'First frame extraction failed'}
   $firstRatio=Magenta-Ratio $frame $crop
   $centerCrop=[Drawing.Rectangle]::new([int]($monitor.Width/2-150),[int]($monitor.Height/2-150),300,300)
   $countdownRatio=Magenta-Ratio $frame $centerCrop
   Assert ($countdownRatio-gt 0.95) 'Countdown panel absent from first recordedframe'
   $expectExcluded=$currentScenario-eq 'Exclusion'
   Assert ($(if($expectExcluded){$ratio-gt 0.95-and $firstRatio-gt 0.95}else{$ratio-lt 0.8-and $firstRatio-lt 0.8})) ('Actualbar exclusion pixel proof '+$currentScenario)
   @{scenario=$currentScenario;windowVisible=$true;crop=@{x=$crop.X;y=$crop.Y;width=$crop.Width;height=$crop.Height};liveMagentaRatio=$ratio;firstFrameMagentaRatio=$firstRatio;countdownCenterMagentaRatio=$countdownRatio;expectedExcluded=$expectExcluded}|ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $outputDirectory 'bar-exclusion.json')
  }
  Close-TestApp;$app=$null;Log ('PASS scenario '+$currentScenario)
 }
 Log 'PASS UX1.1 real UI flow completed'
}catch{Log ('FAIL '+$_);throw}
finally{
 if($app-and !$app.HasExited){$app.Kill();$app.WaitForExit();Log 'Stopped only the test-owned application'}
 if($background-and !$background.HasExited){$background.Kill();$background.WaitForExit()}
 if($settingsTouched){if($hadSettings){[IO.File]::WriteAllBytes($settingsPath,$backup)}elseif(Test-Path -LiteralPath $settingsPath){Remove-Item -LiteralPath $settingsPath};Log 'Restored pre-test user settings'}
 Log ('ARTIFACT '+$artifacts)
}






