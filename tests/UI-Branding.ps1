param([string]$Repository='C:\Users\ViniciusAbreudeOlive\source\repos\Eagle-Eyes',[switch]$TrayGuard,[switch]$RequireBrandingReport)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class EagleBrandNative { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y); [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra); [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l); [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l); }'
$artifacts=Join-Path $Repository ('.test-artifacts\brand-ui-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Force -Path $artifacts|Out-Null
$log=Join-Path $artifacts 'flow.log';$checks=[Collections.Generic.List[string]]::new();$report=@{};$app=$null;$settingsTouched=$false;$failure=$null
function Log([string]$message){$line=(Get-Date -Format o)+' '+$message;Add-Content -Encoding UTF8 -LiteralPath $log -Value $line;Write-Host $line}
function Assert([bool]$value,[string]$message){if(!$value){throw $message};$checks.Add($message);Log ('PASS '+$message)}
$settingsPath=Join-Path $env:LOCALAPPDATA 'EagleEyes\settings.json';$hadSettings=Test-Path -LiteralPath $settingsPath;$backup=if($hadSettings){[IO.File]::ReadAllBytes($settingsPath)}else{$null}
function Elements{
 if(!$app-or $app.HasExited){return @()}
 $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$app.Id)
 $windows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,$condition)
 $result=[Collections.Generic.List[object]]::new();foreach($window in $windows){$result.Add($window);foreach($element in $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)){$result.Add($element)}};return $result.ToArray()
}
function Find([string]$name='',[string]$id='',[string]$kind=''){
 foreach($element in (Elements)){if($name-and $element.Current.Name-ne $name){continue};if($id-and $element.Current.AutomationId-ne $id){continue};if($kind-and $element.Current.ControlType.ProgrammaticName-ne ('ControlType.'+$kind)){continue};if($kind-in @('Button','MenuItem')-and !$element.Current.IsEnabled){continue};return $element};return $null
}
function Wait([string]$name='',[string]$id='',[string]$kind='', [int]$seconds=10){$until=(Get-Date).AddSeconds($seconds);do{$element=Find -name $name -id $id -kind $kind;if($null-ne $element){return $element};Start-Sleep -Milliseconds 100}while((Get-Date)-lt $until);Elements|ForEach-Object {[pscustomobject]@{Name=$_.Current.Name;Id=$_.Current.AutomationId;Type=$_.Current.ControlType.ProgrammaticName}}|ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $artifacts 'failed-uia.json');throw ('Element not found '+$name+' '+$id)}
function Invoke([string]$name,[string]$kind='Button'){$element=Wait -name $name -kind $kind;$element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Log ('Invoked '+$name)}
function Window-For($element){while($element-and $element.Current.ControlType-ne [System.Windows.Automation.ControlType]::Window){$element=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($element)};return $element}
function Native-Icons($window,[string]$label){$hwnd=[IntPtr]$window.Current.NativeWindowHandle;$small=[EagleBrandNative]::SendMessage($hwnd,0x7f,[IntPtr]::Zero,[IntPtr]::Zero);$large=[EagleBrandNative]::SendMessage($hwnd,0x7f,[IntPtr]1,[IntPtr]::Zero);Assert ($small-ne [IntPtr]::Zero-and $large-ne [IntPtr]::Zero) ($label+' exposes native small and large HICON');$report[$label+'Icons']=@{hwnd=$hwnd.ToInt64();small=$small.ToInt64();large=$large.ToInt64()};return $hwnd}
function Tray-Menu([IntPtr]$hwnd){Assert ([EagleBrandNative]::PostMessage($hwnd,0x8056,[IntPtr]1,[IntPtr]0x205)) 'Native tray callback accepted';$null=Wait -name 'Mostrar controles' -kind MenuItem;$null=Wait -name Sair -kind MenuItem}
function Snapshot($window,[string]$name){$rect=$window.Current.BoundingRectangle;$ffmpeg=Join-Path $Repository 'components\ffmpeg-master-latest-win64-gpl\bin\ffmpeg.exe';& $ffmpeg -v error -nostdin -n -f gdigrab -framerate 15 -draw_mouse 0 -offset_x ([int]$rect.Left) -offset_y ([int]$rect.Top) -video_size (([int]$rect.Width).ToString()+'x'+([int]$rect.Height)) -i desktop -frames:v 1 -update 1 (Join-Path $artifacts $name);if($LASTEXITCODE-ne 0){throw 'Screenshot capture failed'}}
try{
 if(Get-Process EagleEyes.App -ErrorAction SilentlyContinue){throw 'BLOCKED: existing user Eagle Eyes instance preserved'}
 New-Item -ItemType Directory -Force -Path (Split-Path $settingsPath)|Out-Null
 $initial=Join-Path $artifacts 'initial-output';New-Item -ItemType Directory -Force -Path $initial|Out-Null
 @{OutputDirectory=$initial;FfmpegDirectory='C:\legacy-ignored-folder';MonitorDevice='';Edge='Right';Offset=.45;Expanded=$true;KeepExpanded=$true;FullMonitor=$false}|ConvertTo-Json|Set-Content -Encoding UTF8 -LiteralPath $settingsPath;$settingsTouched=$true
 $brandingReport=Join-Path $artifacts 'branding-report.json';$exe=Join-Path $Repository 'src\EagleEyes.App\bin\Debug\net11.0-windows\EagleEyes.App.exe'
 $app=Start-Process -FilePath $exe -ArgumentList @('--ux-test','--branding-report',$brandingReport) -WindowStyle Hidden -PassThru;Log ('Launched test PID '+$app.Id)
 $main=Window-For (Wait -name 'Gravar vídeo' -kind Button);$mainHandle=Native-Icons $main 'main'
 Invoke 'Configurações';$settings=Wait -name 'Eagle Eyes · Configurações' -kind Window;$null=Native-Icons $settings 'settings'
 $descendants=$settings.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $edits=@($descendants|Where-Object {$_.Current.ControlType-eq [System.Windows.Automation.ControlType]::Edit});Assert ($edits.Count-eq 1) 'Settings has only output-folder textfield'
 Assert (@($descendants|Where-Object {$_.Current.Name-match '(?i)ffmpeg|ffprobe'}).Count-eq 0) 'Settings exposes no FFmpeg folder or technical component labels'
 Assert (@($descendants|Where-Object {$_.Current.ControlType-eq [System.Windows.Automation.ControlType]::Image}).Count-ge 1) 'Settings displays official raster brand image'
 $output=Wait -id OutputDirectory;$monitor=Wait -id MonitorChoice;Assert (!$monitor.Current.IsEnabled) 'Region mode keeps monitor selector disabled'
 Snapshot $settings 'settings.png'
 $mode=Wait -id CaptureMode;$mode.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();$whole=Wait -name 'Monitor inteiro' -kind ListItem;$itemRect=$whole.Current.BoundingRectangle;[EagleBrandNative]::SetCursorPos([int]($itemRect.Left+$itemRect.Width/2),[int]($itemRect.Top+$itemRect.Height/2))|Out-Null;Start-Sleep -Milliseconds 80;[EagleBrandNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 80;[EagleBrandNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 100
 Assert ((Wait -id MonitorChoice).Current.IsEnabled) 'Full-monitor mode enables monitor selector'
 $newOutput=Join-Path $artifacts 'saved-output';New-Item -ItemType Directory -Force -Path $newOutput|Out-Null;$output.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($newOutput)
 $pin=Wait -id KeepExpanded;$toggle=$pin.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern);if($toggle.Current.ToggleState-eq [System.Windows.Automation.ToggleState]::On){$toggle.Toggle()}
 Invoke Salvar;$null=Wait -name 'Gravar vídeo' -kind Button;$saved=Get-Content -Raw -LiteralPath $settingsPath|ConvertFrom-Json
 Assert ($saved.OutputDirectory-eq $newOutput-and $saved.FullMonitor-and !$saved.KeepExpanded) 'Save persists output capturemode and pin preferences'
 Assert ($saved.PSObject.Properties.Name-notcontains 'FfmpegDirectory') 'Legacy FFmpeg location is ignored and removed on save'
 $savedBytes=[IO.File]::ReadAllBytes($settingsPath)
 Invoke 'Configurações';$output=Wait -id OutputDirectory;$output.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue((Join-Path $artifacts 'cancelled-output'));Invoke Cancelar;$null=Wait -name 'Gravar vídeo' -kind Button
 Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes($settingsPath))-eq [Convert]::ToBase64String($savedBytes)) 'Cancel leaves persisted preferences unchanged'
 Invoke 'Recolher barra';$null=Wait -name 'Expandir barra' -kind Button
 Assert ([EagleBrandNative]::PostMessage($mainHandle,0x8056,[IntPtr]1,[IntPtr]0x203)) 'Native tray doubleclick callback accepted';$null=Wait -name 'Gravar vídeo' -kind Button;Assert ($null-ne (Find -name 'Recolher barra' -kind Button)) 'Tray ShowControls expands idle toolbar'
 Tray-Menu $mainHandle;Invoke 'Mostrar controles' MenuItem
 if($RequireBrandingReport){$until=(Get-Date).AddSeconds(8);while(!(Test-Path -LiteralPath $brandingReport)-and (Get-Date)-lt $until){Start-Sleep -Milliseconds 100};Assert (Test-Path -LiteralPath $brandingReport) 'Branding report created';$brand=Get-Content -Raw -LiteralPath $brandingReport|ConvertFrom-Json;$report.branding=$brand;Assert ($brand.trayRegistered) 'Shell notification icon registered'}
 if($TrayGuard){
  Invoke 'Gravar vídeo';Invoke Gravar;$null=Wait -name 'Pausar gravação' -kind Button -seconds 15;Start-Sleep -Milliseconds 700
  Tray-Menu $mainHandle;Invoke Sair MenuItem;$null=Wait -name 'Descartar gravação?' -kind Window;Invoke 'Continuar gravação';$null=Wait -name 'Pausar gravação' -kind Button;Assert (!$app.HasExited) 'Cancelling tray exit restores recording and keeps app alive'
  Tray-Menu $mainHandle;Invoke Sair MenuItem;$dialog=Wait -name 'Descartar gravação?' -kind Window
  $confirm=$dialog.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Descartar gravação'),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)));$confirm.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
  Assert ($app.WaitForExit(5000)) 'Confirmed tray exit discards recording before closing'
  Assert (@(Get-ChildItem -LiteralPath $newOutput -Filter '*.mp4').Count-eq 0) 'Tray discard creates no finalMP4'
 }else{Tray-Menu $mainHandle;Invoke Sair MenuItem;Assert ($app.WaitForExit(5000)) 'Tray exit closes idle app normally'}
 Log 'PASS branding settings and tray callback flow'
}catch{$failure=$_.ToString();Log ('FAIL '+$failure);throw}
finally{
 if($app-and !$app.HasExited){$app.Kill();$app.WaitForExit();Log 'Stopped only test-owned application'}
 if($settingsTouched){if($hadSettings){[IO.File]::WriteAllBytes($settingsPath,$backup)}elseif(Test-Path -LiteralPath $settingsPath){Remove-Item -LiteralPath $settingsPath};Log 'Restored original preferences'}
 @{checks=$checks.ToArray();report=$report;failure=$failure}|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 (Join-Path $artifacts 'result.json');Log ('ARTIFACT '+$artifacts)
}

