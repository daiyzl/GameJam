param([string]$ProjectPath='C:\Users\DLQian\GameJam',[switch]$OpenEditor,[string]$OutputDirectory,[switch]$InstallOnly)
$ErrorActionPreference='Stop'
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'))) { throw 'Expected existing Unity project' }
$targetScripts=Join-Path $ProjectPath 'Assets\Scripts\EmergenceV1'
$targetEditor=Join-Path $ProjectPath 'Assets\World\EmergenceV1\Editor'
$targetArt=Join-Path $ProjectPath 'Assets\World\EmergenceV1\Art'
New-Item -ItemType Directory -Force -Path $targetArt | Out-Null
$targetAudio=Join-Path $ProjectPath 'Assets\Resources\RiverbankMusic'
New-Item -ItemType Directory -Force -Path $targetAudio | Out-Null
foreach($audio in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Audio') -Filter '*.wav')
{Copy-Item -LiteralPath $audio.FullName -Destination (Join-Path $targetAudio $audio.Name) -Force}
foreach($shader in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Art') | Where-Object { $_.Extension -in @('.shader','.png') })
{Copy-Item -LiteralPath $shader.FullName -Destination (Join-Path $targetArt $shader.Name) -Force}
New-Item -ItemType Directory -Force -Path $targetScripts,$targetEditor | Out-Null
foreach($sourceFolder in @('Scripts','Editor')) {
    $destination=if($sourceFolder -eq 'Scripts'){$targetScripts}else{$targetEditor}
    foreach($source in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $sourceFolder) -Filter '*.cs') {
        $target=Join-Path $destination $source.Name
        if(Test-Path -LiteralPath $target) {
            $savedHash=Join-Path $PSScriptRoot ('Verification\installed-'+$source.Name+'.hash')
            $current=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
            if(-not (Test-Path -LiteralPath $savedHash) -and $current -ne (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash) { throw ('Existing unrelated file '+$target+'; preserve before replacing') }
            if((Test-Path -LiteralPath $savedHash) -and $current -ne (Get-Content -LiteralPath $savedHash -Raw).Trim()) { throw ('User changed '+$target+'; preserve before replacing') }
        }
        Copy-Item -LiteralPath $source.FullName -Destination $target -Force
        (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash | Set-Content -LiteralPath (Join-Path $PSScriptRoot ('Verification\installed-'+$source.Name+'.hash'))
    }
}
$requestRoot=Join-Path $ProjectPath 'EmergenceV1Verification'
$outputRoot=if($OutputDirectory){$OutputDirectory}else{Join-Path $PSScriptRoot 'Verification\UnityPlay'}
New-Item -ItemType Directory -Force -Path $requestRoot,$outputRoot | Out-Null
if(-not $InstallOnly){@{outputDirectory=$outputRoot} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $requestRoot 'request.json') -Encoding UTF8}
if($OpenEditor) {
    $running=Get-Process Unity -ErrorAction SilentlyContinue
    if(-not $running) { Start-Process -FilePath 'D:\Unity\Unity editor\2022.3.62f2c1\Editor\Unity.exe' -ArgumentList '-projectPath',('"'+$ProjectPath+'"'),'-logFile',('"'+(Join-Path $outputRoot 'editor.log')+'"') -WindowStyle Hidden }
}
Write-Output ('Installed V1 scripts only. Verification report: '+(Join-Path $outputRoot 'report.json'))
