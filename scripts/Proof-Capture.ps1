$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$binaryRoot=Join-Path $repoRoot 'components\ffmpeg-master-latest-win64-gpl\bin'
$proofRoot=Join-Path $repoRoot ('.test-artifacts\baseline-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $proofRoot -Force | Out-Null
$clock=[Diagnostics.Stopwatch]::StartNew()
for($i=0;$i -lt 3;$i++){
    $segment=Join-Path $proofRoot "segment-$i.mkv"
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName=Join-Path $binaryRoot 'ffmpeg.exe'
    $start.Arguments="-hide_banner -loglevel error -f gdigrab -framerate 15 -draw_mouse 1 -offset_x 0 -offset_y 0 -video_size 640x360 -i desktop -an -c:v libx264 -preset ultrafast -crf 18 -pix_fmt yuv420p -y `"$segment`""
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.RedirectStandardInput=$true
    $start.RedirectStandardError=$true
    $process=[Diagnostics.Process]::Start($start)
    $errors=$process.StandardError.ReadToEndAsync()
    Start-Sleep -Milliseconds 2200
    if($process.HasExited){throw "FFmpeg encerrou: $($errors.Result)"}
    $process.StandardInput.WriteLine('q')
    $process.StandardInput.Flush()
    if(-not $process.WaitForExit(15000)){ $process.Kill(); throw 'Timeout fechando segmento; temporários preservados.' }
    [IO.File]::WriteAllText((Join-Path $proofRoot "segment-$i.log"),$errors.Result)
    if($process.ExitCode -ne 0){throw "Falha segmento $i"}
    $process.Dispose()
    if($i -lt 2){Start-Sleep -Milliseconds 1500}
}
$list=Join-Path $proofRoot 'concat.txt'
[IO.File]::WriteAllLines($list,@("file 'segment-0.mkv'","file 'segment-1.mkv'","file 'segment-2.mkv'"),[Text.UTF8Encoding]::new($false))
$output=Join-Path $proofRoot 'proof.mp4'
& (Join-Path $binaryRoot 'ffmpeg.exe') -hide_banner -loglevel error -f concat -safe 0 -i $list -map 0:v:0 -an -c copy -movflags +faststart $output
if($LASTEXITCODE -ne 0){throw 'Concat falhou; segmentos preservados.'}
$json=& (Join-Path $binaryRoot 'ffprobe.exe') -v error -show_streams -show_format -of json $output
if($LASTEXITCODE -ne 0){throw 'ffprobe falhou'}
$json | Set-Content (Join-Path $proofRoot 'probe.json') -Encoding UTF8
$probe=($json -join "`n") | ConvertFrom-Json
if(@($probe.streams | Where-Object codec_type -eq audio).Count -ne 0){throw 'Áudio inesperado'}
if(@($probe.streams | Where-Object codec_type -eq video).Count -ne 1){throw 'Vídeo inválido'}
$duration=[double]::Parse($probe.format.duration,[Globalization.CultureInfo]::InvariantCulture)
if($duration -ge $clock.Elapsed.TotalSeconds-2){throw 'Duração não exclui pausas como esperado'}
& (Join-Path $binaryRoot 'ffmpeg.exe') -v error -i $output -f null -
if($LASTEXITCODE -ne 0){throw 'Decodificação falhou'}
[PSCustomObject]@{Output=$output;Duration=$duration;WallTime=$clock.Elapsed.TotalSeconds;AudioStreams=0;VideoStreams=1;FPS=$probe.streams[0].r_frame_rate;Decode='PASS'} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $proofRoot 'result.json')
