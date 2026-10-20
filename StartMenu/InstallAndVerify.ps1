param([string]$ProjectPath='C:/Users/DLQian/GameJam',[string]$OutputDirectory,[switch]$InstallOnly)
$ErrorActionPreference='Stop'
$destinationRuntime=Join-Path $ProjectPath 'Assets/Scripts/StartMenu'
$destinationEditor=Join-Path $ProjectPath 'Assets/World/StartMenu/Editor'
New-Item -ItemType Directory -Force -Path $destinationRuntime,$destinationEditor | Out-Null
$fontDestination=Join-Path $ProjectPath 'Assets/World/StartMenu/Fonts'
New-Item -ItemType Directory -Force -Path $fontDestination | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'UI/Fonts/*') -Destination $fontDestination -Force
foreach($menuFolder in @('Runtime','Editor')) {
    $destination=if($menuFolder -eq 'Runtime'){$destinationRuntime}else{$destinationEditor}
    foreach($source in Get-ChildItem (Join-Path $PSScriptRoot $menuFolder) -Filter '*.cs') {
        Copy-Item -LiteralPath $source.FullName -Destination (Join-Path $destination $source.Name) -Force
    }
}
foreach($menuFile in @('StartEnum.uxml','StartMenu.uss','StartMenuTheme.tss')) {
    $destination=if($menuFile -eq 'StartMenuTheme.tss'){Join-Path $ProjectPath 'Assets/World/StartMenu/StartMenuTheme.tss'}else{Join-Path (Join-Path $ProjectPath 'Assets/World') $menuFile}
    $source=Join-Path (Join-Path $PSScriptRoot 'UI') $menuFile
    $savedHash=Join-Path (Join-Path $PSScriptRoot 'Verification') ('installed-'+$menuFile+'.hash')
    if((Test-Path $destination) -and (Test-Path $savedHash) -and (Get-FileHash $destination).Hash -ne (Get-Content $savedHash -Raw).Trim()) { throw ('Edited menu asset; merge before installing: '+$destination) }
    Copy-Item -LiteralPath $source -Destination $destination -Force
    (Get-FileHash $destination).Hash | Set-Content $savedHash
}
foreach($menuGameplayFile in @('V1HUD.cs','V1World.cs')) {
    $source=Join-Path (Split-Path $PSScriptRoot -Parent) ('EmergenceV1/Scripts/'+$menuGameplayFile)
    $destination=Join-Path $ProjectPath ('Assets/Scripts/EmergenceV1/'+$menuGameplayFile)
    $hashFile=Join-Path (Split-Path $PSScriptRoot -Parent) ('EmergenceV1/Verification/installed-'+$menuGameplayFile+'.hash')
    if((Get-FileHash $destination).Hash -ne (Get-Content $hashFile -Raw).Trim()) { throw ('Edited game source; merge before installing: '+$destination) }
    Copy-Item -LiteralPath $source -Destination $destination -Force
    (Get-FileHash $destination).Hash | Set-Content $hashFile
}
$output=if($OutputDirectory){$OutputDirectory}else{Join-Path $PSScriptRoot 'Verification/20261014/UnityPlay'}
$requestFolder=Join-Path $ProjectPath 'StartMenuVerification'
New-Item -ItemType Directory -Force -Path $output,$requestFolder | Out-Null
if(-not $InstallOnly){@{outputDirectory=$output} | ConvertTo-Json | Set-Content (Join-Path $requestFolder 'request.json') -Encoding UTF8}
Write-Output ('Menu verification report: '+(Join-Path $output 'report.json'))
