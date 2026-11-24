param(
 [string]$Repository='C:\Users\ViniciusAbreudeOlive\source\repos\Eagle-Eyes',
 [ValidateSet('Functional','Exclusion','ExclusionControl','Drag','TooltipsControl','Tooltips','RepeatedActions','Presentation','All')][string]$Scenario='All'
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
 foreach($window in $windows){
  try{$children=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)}
  catch [System.Windows.Automation.ElementNotAvailableException]{continue}
  $result.Add($window)
  foreach($child in $children){$result.Add($child)}
 }
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
function Save-Recording([bool]$alreadyRequested=$false){
 if(!$alreadyRequested){Invoke-Button 'Parar e salvar'}
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
function Invoke-Twice($pattern,[string]$label){
 $pattern.Invoke();Log ('First invoke '+$label)
 try{$pattern.Invoke();Log ('Repeated invoke delivered '+$label+'; state must reject duplicate')}
 catch [System.Windows.Automation.ElementNotAvailableException]{Log ('Repeated invoke rejected: element removed '+$label)}
 catch [System.Windows.Automation.ElementNotEnabledException]{Log ('Repeated invoke rejected: element disabled '+$label)}
 catch [InvalidOperationException]{Log ('Repeated invoke rejected: unavailable '+$label)}
}
function White-Ratio([string]$path,[System.Drawing.Rectangle]$crop){
 $bitmap=[Drawing.Bitmap]::new($path);try{$count=0;$total=0;for($y=$crop.Top;$y-lt $crop.Bottom;$y+=2){for($x=$crop.Left;$x-lt $crop.Right;$x+=2){$pixel=$bitmap.GetPixel($x,$y);if($pixel.R-gt 210-and $pixel.G-gt 210-and $pixel.B-gt 210){$count++};$total++}};return $count/[double]$total}finally{$bitmap.Dispose()}
}
function Wait-VisibleTooltip([string]$expectedName='*'){
 $until=(Get-Date).AddSeconds(8)
 do{
  foreach($element in (Elements)){
   if($element.Current.ControlType-eq [System.Windows.Automation.ControlType]::ToolTip-and !$element.Current.IsOffscreen -and $element.Current.Name -like $expectedName){return $element}
  }
  Start-Sleep -Milliseconds 100
 }while((Get-Date)-lt $until)
 throw 'No visible UIAutomation ToolTip after hover'
}

function Assert-Hint([string]$name,[string]$expected){
 $button=Wait-Element -name $name -kind Button
 $rect=$button.Current.BoundingRectangle
 [EagleUiInput]::SetCursorPos([int]($rect.Left+$rect.Width/2),[int]($rect.Top+$rect.Height/2))|Out-Null
 $tooltip=Wait-VisibleTooltip $expected
 Assert ($tooltip.Current.Name -like $expected -and $button.Current.HelpText -eq $tooltip.Current.Name) ("Visible and accessible hint for "+$name)
}
function Sample-Tooltip([string]$label,$hoverTarget){
 [EagleUiInput]::SetCursorPos(($monitor.Left+100),($monitor.Top+$monitor.Height-150))|Out-Null;Start-Sleep -Milliseconds 800
 $target=$hoverTarget.Current.BoundingRectangle
 [EagleUiInput]::SetCursorPos([int]($target.Left+$target.Width/2),[int]($target.Top+$target.Height/2))|Out-Null
 $tooltip=Wait-VisibleTooltip;Start-Sleep -Milliseconds 350;$tooltip=Wait-VisibleTooltip;$rect=$tooltip.Current.BoundingRectangle
 Assert ($rect.Width-gt 8-and $rect.Height-gt 8) ('Visible tooltip rectangle '+$label)
 $crop=[Drawing.Rectangle]::new([int]($rect.Left-$monitor.Left-2),[int]($rect.Top-$monitor.Top-2),[int]($rect.Width+4),[int]($rect.Height+4))
 $image=Join-Path $outputDirectory ('tooltip-'+$label+'-gdigrab.png');Capture-Screen $image
 $ratio=Magenta-Ratio $image $crop
 $liveWhite=White-Ratio $image $crop
 $sessionFile=Get-ChildItem -LiteralPath $outputDirectory -Recurse -Filter session.json|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1
 $sessionData=Get-Content -Raw -LiteralPath $sessionFile.FullName|ConvertFrom-Json
 $started=[DateTimeOffset]::Parse($sessionData.Started)
 $sampleSecond=[Math]::Round(([DateTimeOffset]::Now-$started).TotalSeconds+0.5,3)
 Start-Sleep -Milliseconds 2000
 $tooltipAfter=Wait-VisibleTooltip
 Assert (!$tooltipAfter.Current.IsOffscreen) ('Tooltip stayed visible for video sample '+$label)
 Log ('Tooltip '+$label+' name='+$tooltip.Current.Name+' sampleSecond='+$sampleSecond+' liveMagenta='+$ratio)
 return [pscustomobject]@{label=$label;tooltipName=$tooltip.Current.Name;crop=@{x=$crop.X;y=$crop.Y;width=$crop.Width;height=$crop.Height};sampleSecond=$sampleSecond;timestampSource='session Started and wall time plus0.5sec';liveMagentaRatio=$ratio;liveWhiteRatio=$liveWhite}
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
 $scenarios=if($Scenario-eq 'All'){@('Functional','ExclusionControl','Exclusion','Drag','TooltipsControl','Tooltips','RepeatedActions')}else{@($Scenario)}
 foreach($currentScenario in $scenarios){
  $outputDirectory=Join-Path $artifacts $currentScenario;New-Item -ItemType Directory -Force -Path $outputDirectory|Out-Null
  @{OutputDirectory=$outputDirectory;FfmpegDirectory=(Join-Path $Repository 'components\ffmpeg-master-latest-win64-gpl\bin');MonitorDevice='';Edge='Right';Offset=0.45;Expanded=$true;KeepExpanded=$true;FullMonitor=$false;HideDuringCapture=$false;Countdown=3}|ConvertTo-Json|Set-Content -Encoding UTF8 -LiteralPath $settingsPath
  $settingsTouched=$true;$arguments=if($currentScenario-in @('ExclusionControl','TooltipsControl')){'--ux-test --disable-capture-exclusion'}else{'--ux-test'}
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
  }elseif($currentScenario-in @('TooltipsControl','Tooltips')){
   Select-Area $true;Invoke-Button 'Gravar';Observe-Countdown;Start-Sleep -Seconds 2
   $samples=[Collections.Generic.List[object]]::new()
   $samples.Add((Sample-Tooltip 'background' (Wait-Element -id RecordingStatus)))
   $samples.Add((Sample-Tooltip 'pause' (Wait-Element -name 'Pausar gravação' -kind Button)))
   $samples.Add((Sample-Tooltip 'duration' (Wait-Element -id RecordingDuration)))
   [EagleUiInput]::SetCursorPos(($monitor.Left+100),($monitor.Top+$monitor.Height-150))|Out-Null;Start-Sleep -Milliseconds 300
   $output=Save-Recording
   $expectExcluded=$currentScenario-eq 'Tooltips'
   foreach($sample in $samples){
    $frame=Join-Path $outputDirectory ('tooltip-'+$sample.label+'-mp4.png')
    & $ffmpeg -v error -nostdin -n -ss ($sample.sampleSecond.ToString('0.000',[Globalization.CultureInfo]::InvariantCulture)) -i $output -frames:v 1 -update 1 $frame;if($LASTEXITCODE-ne 0){throw 'Tooltip video frame extraction failed'}
    $crop=[Drawing.Rectangle]::new($sample.crop.x,$sample.crop.y,$sample.crop.width,$sample.crop.height)
    $ratio=Magenta-Ratio $frame $crop
    $sample|Add-Member -NotePropertyName videoMagentaRatio -NotePropertyValue $ratio
    $sample|Add-Member -NotePropertyName videoWhiteRatio -NotePropertyValue (White-Ratio $frame $crop)
    $bitmap=[Drawing.Bitmap]::new($frame)
    try{$preview=$bitmap.Clone($crop,[Drawing.Imaging.PixelFormat]::Format32bppArgb);try{$preview.Save((Join-Path $outputDirectory ('tooltip-'+$sample.label+'-mp4-crop.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$preview.Dispose()}}finally{$bitmap.Dispose()}
    $sample|Add-Member -NotePropertyName expectedExcluded -NotePropertyValue $expectExcluded
   }
   $samples.ToArray()|ConvertTo-Json -Depth 6|Set-Content -Encoding UTF8 (Join-Path $outputDirectory 'tooltip-exclusion.json')
   foreach($sample in $samples){Assert ($(if($expectExcluded){$sample.liveMagentaRatio-gt 0.95-and $sample.videoMagentaRatio-gt 0.95-and $sample.videoWhiteRatio-lt 0.1}else{$sample.liveMagentaRatio-lt 0.8-and $sample.videoMagentaRatio-lt 0.8-and $sample.liveWhiteRatio-gt 0.45-and $sample.videoWhiteRatio-gt 0.45})) ('Tooltip gdigrab andMP4 pixel proof '+$currentScenario+' '+$sample.label)}
  }elseif($currentScenario-eq 'RepeatedActions'){
   Select-Area $true
   $start=Wait-Element -name Gravar -kind Button;$pattern=$start.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
   Invoke-Twice $pattern 'Gravar'
   $countdown=Wait-Element -name 'Eagle Eyes · Contagem' -kind Window
   $count=@(Elements|Where-Object {$_.Current.ControlType-eq [System.Windows.Automation.ControlType]::Window-and $_.Current.Name-eq 'Eagle Eyes · Contagem'}).Count
   Assert ($count-eq 1) 'Two start invokes expose exactly one countdownwindow'
   Observe-Countdown;Start-Sleep -Seconds 2
   $sessions=@(Get-ChildItem -LiteralPath $outputDirectory -Directory -Filter '.eagle-eyes-session-*')
   Assert ($sessions.Count-eq 1) 'Two start invokes create exactly one recording session'
   $save=Wait-Element -name 'Parar e salvar' -kind Button;$pattern=$save.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
   Invoke-Twice $pattern 'Parar e salvar'
   $output=Save-Recording $true
   Start-Sleep -Milliseconds 300
   Assert (@(Get-ChildItem -LiteralPath $outputDirectory -Filter '*.mp4').Count-eq 1) 'Two save invokes publish exactly one MP4'
   Assert (@(Get-ChildItem -LiteralPath $outputDirectory -Directory -Filter '.eagle-eyes-session-*').Count-eq 1) 'Repeated commands never create another session'
  }elseif($currentScenario-eq 'Presentation'){
   Click-Button 'Tirar print (Ctrl+Shift+S)'
   $null=Wait-Element -name 'Editar print' -kind Button
   Start-Sleep -Seconds 4
   Assert ([Windows.Forms.Clipboard]::ContainsImage()) 'Idle camera copies a pasteable bitmap'
   Assert (@(Get-ChildItem $outputDirectory -Filter 'Print-*.png').Count -eq 1) 'Idle thumbnail expires and saves once'
   Hotkey 0x53
   $null=Wait-Element -name 'Editar print' -kind Button
   Start-Sleep -Milliseconds 350
   Click-Button 'Editar print'
   Assert-Hint 'Desativar lápis' '*novamente para parar de desenhar*'
   Click-Button 'Desativar lápis'
   Drag-Point ([int]($monitor.Width/2)) ([int]($monitor.Height/2)) 100 10
   $undo=Wait-Element -name 'Desfazer'
   Assert (!$undo.Current.IsEnabled) 'Dragging with pencil off leaves the print untouched'
   Click-Button 'Ativar lápis'
   Drag-Point ([int]($monitor.Width/2)) ([int]($monitor.Height/2)) 90 15
   Click-Button 'Desfazer'
   Assert (!$undo.Current.IsEnabled) 'Undo remains accessible with pencil active'
   Click-Button 'Desativar lápis'
   Click-Button 'Lápis vermelho'
   Start-Sleep -Seconds 3
   Assert (@(Get-ChildItem $outputDirectory -Filter 'Print-*.png').Count -eq 1) 'Editor cancels automatic save'
   Drag-Point ([int]($monitor.Width/2)) ([int]($monitor.Height/2)) 120 20
   Click-Button 'Salvar print'
   Start-Sleep -Milliseconds 800
   $files=@(Get-ChildItem $outputDirectory -Filter 'Print-*.png' | Sort-Object CreationTimeUtc)
   Assert ($files.Count -eq 2) 'Edited print saves once'
   $bitmap=[Drawing.Bitmap]::new($files[1].FullName)
   $paste=[Windows.Forms.Clipboard]::GetImage()
   try {
    $found=$false
    for($y=[int]($bitmap.Height/2)-15;$y -lt $bitmap.Height/2+75 -and !$found;$y++){
     for($x=[int]($bitmap.Width/2);$x -lt $bitmap.Width/2+180;$x++){
      $pixel=$bitmap.GetPixel($x,$y)
      if($pixel.B -eq 0 -and $pixel.G -eq 0 -and $pixel.R -eq 255){$found=$paste.GetPixel($x,$y).ToArgb() -eq $pixel.ToArgb();if($found){break}}
     }
    }
    Assert $found 'Edited red stroke present in PNG and clipboard'
   } finally {$paste.Dispose();$bitmap.Dispose()}
   Select-Area $true;Click-Button 'Gravar';Observe-Countdown
   Assert-Hint 'Tirar print (Ctrl+Shift+S)' '*Ctrl+Shift+S*área da gravação*'
   Hotkey 0x53
   $null=Wait-Element -name 'Editar print' -kind Button
   Start-Sleep -Milliseconds 350
   Click-Button 'Editar print'
   Drag-Point ([int]($monitor.Width/2)) ([int]($monitor.Height/2)) 80 15
   [Windows.Forms.Clipboard]::SetText('clipboard after capture')
   Click-Button 'Descartar print'
   Start-Sleep -Seconds 4
   Assert ($null -eq (Find-Element -name 'Descartar print' -kind Button)) 'Discard closes the editor'
   Assert (@(Get-ChildItem $outputDirectory -Filter 'Print-*.png').Count -eq 2) 'Discard publishes no PNG after the preview timeout'
   Assert ([Windows.Forms.Clipboard]::GetText() -eq 'clipboard after capture') 'Discard preserves the current clipboard'
   Click-Button 'Ativar lápis'
   Drag-Point ([int]($monitor.Width/2)) ([int]($monitor.Height/2)) 90 45
   Click-Button 'Desativar lápis'
   Click-Button 'Ativar lápis'
   Click-Button 'Pausar gravação'
   Click-Button 'Retomar gravação'
   Click-Button 'Parar e salvar'
   $output=Save-Recording $true
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
