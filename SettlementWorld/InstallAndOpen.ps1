$ErrorActionPreference = 'Stop'
$stageRoot = $PSScriptRoot
$projectRoot = 'C:\Users\DLQian\GameJam'
$artRoot = Join-Path $projectRoot 'Assets\World\Settlement'
$scriptRoot = Join-Path $projectRoot 'Assets\Scripts\Settlement'
$editorRoot = Join-Path $artRoot 'Editor'
New-Item -ItemType Directory -Path (Join-Path $artRoot 'Art'),$scriptRoot,$editorRoot -Force | Out-Null
$hashFile = Join-Path $stageRoot 'Verification\installed-hashes.json'
$previousHashes = @{}
if (Test-Path -LiteralPath $hashFile) { $previousHashes = Get-Content -LiteralPath $hashFile -Raw | ConvertFrom-Json -AsHashtable }
$newHashes = @{}
$copies = @()
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Art') -Filter '*.png') { $copies += @{source=$file.FullName;target=(Join-Path (Join-Path $artRoot 'Art') $file.Name)} }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Scripts') -Filter '*.cs') { $copies += @{source=$file.FullName;target=(Join-Path $scriptRoot $file.Name)} }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Editor') -Filter '*.cs') { $copies += @{source=$file.FullName;target=(Join-Path $editorRoot $file.Name)} }
foreach ($name in @('art-prompts.json','README.md')) { if (Test-Path -LiteralPath (Join-Path $stageRoot $name)) { $copies += @{source=(Join-Path $stageRoot $name);target=(Join-Path $artRoot $name)} } }
foreach ($copy in $copies) {
    $sourceHash = (Get-FileHash -LiteralPath $copy.source).Hash
    if (Test-Path -LiteralPath $copy.target) { $currentHash = (Get-FileHash -LiteralPath $copy.target).Hash; if ($currentHash -ne $sourceHash -and $previousHashes[$copy.target] -ne $currentHash) { throw ('Preserve modified project file: '+$copy.target) } }
    Copy-Item -LiteralPath $copy.source -Destination $copy.target
    $newHashes[$copy.target] = $sourceHash
}
$newHashes | ConvertTo-Json | Set-Content -LiteralPath $hashFile -Encoding utf8
$requestDirectory = Join-Path $projectRoot 'SettlementVerification'
New-Item -ItemType Directory -Path $requestDirectory -Force | Out-Null
$outputDirectory = Join-Path $stageRoot 'Verification\Final'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
@{outputDirectory=$outputDirectory} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $requestDirectory 'request.json') -Encoding utf8
$existing = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like '*C:\Users\DLQian\GameJam*' }
if ($existing) { Write-Output 'Installed settlement world. Existing Unity editor will import, build and verify it.' }
else { Start-Process -FilePath 'D:\Unity\Unity editor\2022.3.62f2c1\Editor\Unity.exe' -ArgumentList @('-projectPath',('"'+$projectRoot+'"')) -WindowStyle Normal; Write-Output 'Opened the authorized Unity project.' }
