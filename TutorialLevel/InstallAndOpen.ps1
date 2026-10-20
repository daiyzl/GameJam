$ErrorActionPreference = 'Stop'
$stageRoot = $PSScriptRoot
$projectRoot = 'C:\Users\DLQian\GameJam'
$artRoot = Join-Path $projectRoot 'Assets\World\Tutorial'
$scriptRoot = Join-Path $projectRoot 'Assets\Scripts\Tutorial'
$editorRoot = Join-Path $artRoot 'Editor'
New-Item -ItemType Directory -Path (Join-Path $artRoot 'Art'),$scriptRoot,$editorRoot -Force | Out-Null
$hashFile = Join-Path $stageRoot 'Verification\installed-hashes.json'
$previousHashes = @{}
if (Test-Path -LiteralPath $hashFile) { $previousHashes = Get-Content -LiteralPath $hashFile -Raw | ConvertFrom-Json -AsHashtable }
$newHashes = @{}
$copies = @()
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Art') -Filter '*.png') { $copies += @{ source=$file.FullName; target=(Join-Path (Join-Path $artRoot 'Art') $file.Name) } }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Scripts') -Filter '*.cs') { $copies += @{ source=$file.FullName; target=(Join-Path $scriptRoot $file.Name) } }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Editor') -Filter '*.cs') { $copies += @{ source=$file.FullName; target=(Join-Path $editorRoot $file.Name) } }
$copies += @{ source=(Join-Path $stageRoot 'art-prompts.json'); target=(Join-Path $artRoot 'art-prompts.json') }
if (Test-Path -LiteralPath (Join-Path $stageRoot 'README.md')) { $copies += @{ source=(Join-Path $stageRoot 'README.md'); target=(Join-Path $artRoot 'README.md') } }
foreach ($copy in $copies) {
    $sourceHash = (Get-FileHash -LiteralPath $copy.source).Hash
    if (Test-Path -LiteralPath $copy.target) {
        $currentHash = (Get-FileHash -LiteralPath $copy.target).Hash
        if ($currentHash -ne $sourceHash -and $previousHashes[$copy.target] -ne $currentHash) { throw ('Preserve modified project file before installation: ' + $copy.target) }
    }
    Copy-Item -LiteralPath $copy.source -Destination $copy.target
    $newHashes[$copy.target] = $sourceHash
}
$newHashes | ConvertTo-Json | Set-Content -LiteralPath $hashFile -Encoding utf8
$requestDirectory = Join-Path $projectRoot 'TutorialVerification'
New-Item -ItemType Directory -Path $requestDirectory -Force | Out-Null
$verificationOutput = Join-Path $stageRoot 'Verification\Final'
New-Item -ItemType Directory -Path $verificationOutput -Force | Out-Null
@{ outputDirectory=$verificationOutput } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $requestDirectory 'request.json') -Encoding utf8
$existing = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like '*C:\Users\DLQian\GameJam*' }
if ($existing) { Write-Output 'Tutorial installed. Existing Unity editor will import the files and run verification.' }
else {
    Start-Process -FilePath 'D:\Unity\Unity editor\2022.3.62f2c1\Editor\Unity.exe' -ArgumentList @('-projectPath',('"'+$projectRoot+'"'),'-logFile',('"'+(Join-Path $stageRoot 'Verification\editor.log')+'"')) -WindowStyle Normal
    Write-Output 'Opened the authorized Unity project to build and verify the tutorial scene.'
}
