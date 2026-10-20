$ErrorActionPreference = 'Stop'
$stageRoot = Split-Path $PSScriptRoot -Parent
$projectRoot = 'C:\Users\DLQian\GameJam'
$simulationPath = Join-Path $projectRoot 'Assets\Scripts\FiveElementSimulation.cs'
$originalHash = '6B8D5B81482B7E06592AD83D898F6B0E8A92F612E955515F623ED2CA46ABFCFB'
$stagedHash = (Get-FileHash -LiteralPath (Join-Path $stageRoot 'FiveElementSimulation.cs')).Hash
$currentHash = (Get-FileHash -LiteralPath $simulationPath).Hash
if ($currentHash -ne $originalHash -and $currentHash -ne $stagedHash) { throw 'Simulation changed since inspection. Preserve user changes before installation.' }
$backup = Join-Path $stageRoot 'Original'
New-Item -ItemType Directory -Path $backup -Force | Out-Null
if ($currentHash -eq $originalHash) { Copy-Item -LiteralPath $simulationPath -Destination (Join-Path $backup 'FiveElementSimulation.cs') }
Copy-Item -LiteralPath (Join-Path $stageRoot 'FiveElementSimulation.cs') -Destination $simulationPath
$scripts = Join-Path $projectRoot 'Assets\Scripts\World'
$editor = Join-Path $projectRoot 'Assets\World\Editor'
New-Item -ItemType Directory -Path $scripts,$editor -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Scripts') -Filter '*.cs' | Copy-Item -Destination $scripts
Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Editor') -Filter '*.cs' | Copy-Item -Destination $editor
$requestDirectory = Join-Path $projectRoot 'WorldVerification'
New-Item -ItemType Directory -Path $requestDirectory -Force | Out-Null
@{ outputDirectory = (Join-Path $stageRoot 'Verification') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $requestDirectory 'request.json') -Encoding utf8
$unityPath = 'D:\Unity\Unity editor\2022.3.62f2c1\Editor\Unity.exe'
$existing = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like '*C:\Users\DLQian\GameJam*' }
if ($existing) { Write-Output 'Installed into the existing Unity project. Existing editor will import the world scripts.' }
else {
    Start-Process -FilePath $unityPath -ArgumentList @('-projectPath',('"'+$projectRoot+'"'),'-logFile',('"'+(Join-Path $stageRoot 'Verification\editor.log')+'"')) -WindowStyle Normal
    Write-Output 'Opened the authorized GameJam Unity project. Editor will create the world and run Play verification.'
}
