$ErrorActionPreference = 'Stop'
$packRoot = $PSScriptRoot
$projectRoot = 'C:\Users\DLQian\GameJam'
$destination = Join-Path $projectRoot 'Assets\World\PixelArtV1'
if (Test-Path -LiteralPath $destination) { throw 'PixelArtV1 already exists. Use a versioned folder to preserve existing assets.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $packRoot 'Sprites') -Destination $destination -Recurse
Copy-Item -LiteralPath (Join-Path $packRoot 'Editor') -Destination $destination -Recurse
Copy-Item -LiteralPath (Join-Path $packRoot 'prompts.json') -Destination $destination
$reportPath = Join-Path $packRoot 'Verification\unity-import.json'
$logPath = Join-Path $packRoot 'Verification\unity-import.log'
$existing = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like '*C:\Users\DLQian\GameJam*' }
if ($existing) { throw 'Unity editor is already open. Run 五行世界/像素素材/配置导入与切片 in the existing editor.' }
Start-Process -FilePath 'D:\Unity\Unity editor\2022.3.62f2c1\Editor\Unity.exe' -ArgumentList @('-batchmode','-nographics','-projectPath',('"'+$projectRoot+'"'),'-executeMethod','PixelArtPackageBuilder.RunBatch','-pixelArtReport',('"'+$reportPath+'"'),'-logFile',('"'+$logPath+'"'),'-quit') -WindowStyle Hidden -Wait
if (-not (Test-Path -LiteralPath $reportPath)) { throw 'Unity did not produce an import verification report. Inspect the log.' }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($report.status -ne 'passed') { throw 'Unity import verification failed.' }
Write-Output ('PASS: {0} sprites, {1} prefabs, {2} clips installed into {3}' -f $report.spriteCount,$report.prefabCount,$report.animationCount,$destination)
