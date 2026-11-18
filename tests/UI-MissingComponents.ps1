param([string]$Repository='C:\Users\ViniciusAbreudeOlive\source\repos\Eagle-Eyes')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
if(Get-Process EagleEyes.App -ErrorAction SilentlyContinue){throw 'Existing Eagle Eyes instance preserved. Close it before this test.'}
$artifacts=Join-Path $Repository ('.test-artifacts\missing-components-'+[Guid]::NewGuid().ToString('N'))
$isolated=Join-Path ([IO.Path]::GetTempPath()) ('eagle-eyes-isolated-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $artifacts,$isolated,(Join-Path $isolated 'Assets')|Out-Null
if([IO.Path]::GetFullPath($isolated).StartsWith([IO.Path]::GetFullPath($Repository),[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture must be outside development defaults.'}
$built=Join-Path $Repository 'src\EagleEyes.App\bin\Debug\net11.0-windows'
Get-ChildItem -LiteralPath $built -File|Where-Object Extension -NE '.pdb'|Copy-Item -Destination $isolated
Copy-Item -LiteralPath (Join-Path $built 'Assets\eagle-eyes.ico') -Destination (Join-Path $isolated 'Assets')
$app=$null;$failure=$null;$alert='';$passed=0
function Find-Item([string]$name){
 $windows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$app.Id))
 foreach($window in $windows){$result=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name));if($result){return $result}}
 return $null
}
function Wait-Item([string]$name){$until=(Get-Date).AddSeconds(12);do{$item=Find-Item $name;if($item){return $item};Start-Sleep -Milliseconds 100}while((Get-Date)-lt $until);throw ('Missing UI element: '+$name)}
function Invoke-Item([string]$name){$item=Wait-Item $name;$item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
try{
 $app=Start-Process -FilePath (Join-Path $isolated 'EagleEyes.App.exe') -ArgumentList '--ux-test' -WindowStyle Hidden -PassThru
 Invoke-Item 'Gravar vídeo'
 $expected='Os componentes de gravação não foram encontrados. Coloque os arquivos ffmpeg.exe e ffprobe.exe juntos na pasta ffmpeg, ao lado do Eagle Eyes, e tente gravar novamente.'
 $item=Wait-Item $expected;$alert=$item.Current.Name;$passed++
 if(Find-Item 'Gravar'){throw 'Unexpected preparation: missing dependencies were not blocked.'};$passed++
 if(Get-CimInstance Win32_Process -Filter "ParentProcessId=$($app.Id)"|Where-Object Name -EQ 'ffmpeg.exe'){throw 'Encoder unexpectedly started'};$passed++
 Invoke-Item 'Entendi';$null=Wait-Item 'Gravar vídeo';$passed++
 Invoke-Item 'Fechar Eagle Eyes';if(!$app.WaitForExit(5000)){throw 'Test application did not close'};$passed++
 Write-Output 'PASS missing components alert, no preparation/encoder, return to usable toolbar, normal close'
} catch {$failure=$_.ToString();throw}
finally{
 if($app-and !$app.HasExited){$app.Kill();$app.WaitForExit()}
 @{Passed=$passed;Alert=$alert;Failure=$failure;IsolatedApplication=$isolated}|ConvertTo-Json -Depth 4|Set-Content -Encoding UTF8 (Join-Path $artifacts 'result.json')
 Write-Output ('ARTIFACT '+$artifacts)
}
