$ErrorActionPreference = 'Stop'
$stageDirectory = $PSScriptRoot
$projectDirectory = 'C:\Users\DLQian\GameJam'
$scriptsDirectory = Join-Path $projectDirectory 'Assets\Scripts'
$backupHashes = Get-Content -LiteralPath (Join-Path $stageDirectory 'Original\hashes.json') -Raw | ConvertFrom-Json
foreach ($name in @('BaseElement.cs','EnvironmentSnapshot.cs','GameRuler.cs')) {
    if ((Get-FileHash -LiteralPath (Join-Path $scriptsDirectory $name)).Hash -ne $backupHashes.$name) {
        throw ('The user changed a script since inspection: ' + $name)
    }
}
foreach ($name in @('FiveElementSimulation.cs','PrototypeLevels.cs','PrototypeView.cs','SimulationVerification.cs')) {
    if (Test-Path -LiteralPath (Join-Path $scriptsDirectory $name)) { throw ('Inspect existing script before replacing: ' + $name) }
}
$editorDirectory = Join-Path $projectDirectory 'Assets\Prototype\Editor'
New-Item -ItemType Directory -Path $editorDirectory -Force | Out-Null
foreach ($name in @('BaseElement.cs','EnvironmentSnapshot.cs','GameRuler.cs','FiveElementSimulation.cs','PrototypeLevels.cs','PrototypeView.cs','SimulationVerification.cs')) {
    Copy-Item -LiteralPath (Join-Path $stageDirectory ('Scripts\' + $name)) -Destination (Join-Path $scriptsDirectory $name)
}
Copy-Item -LiteralPath (Join-Path $stageDirectory 'Editor\PrototypeEditorBridge.cs') -Destination (Join-Path $editorDirectory 'PrototypeEditorBridge.cs')
Copy-Item -LiteralPath (Join-Path $stageDirectory 'README.md') -Destination (Join-Path $projectDirectory 'Assets\Prototype\README.md')
$requestDirectory = Join-Path $projectDirectory 'PrototypeVerification'
New-Item -ItemType Directory -Path $requestDirectory -Force | Out-Null
@{ id = 'five-elements-first-play'; outputDirectory = (Join-Path $stageDirectory 'Verification') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $requestDirectory 'request.json') -Encoding utf8
$activeEditor = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like '*C:\Users\DLQian\GameJam*' }
if ($activeEditor) { Write-Output ('Using already-open project editor: ' + $activeEditor.ProcessId) }
else {
    # The user explicitly requested reopening this interactive Unity project.
    $editorArguments = @('-projectPath', '"C:\Users\DLQian\GameJam"', '-logFile', ('"' + (Join-Path $stageDirectory 'Verification\editor.log') + '"'))
    $editorProcess = Start-Process -FilePath 'D:\Unity\Unity editor\2022.3.62f2c1\Editor\Unity.exe' -ArgumentList $editorArguments -WindowStyle Normal -PassThru
    Write-Output ('Opened interactive Unity editor: ' + $editorProcess.Id)
}
Write-Output 'Installed prototype and queued in-editor Play verification.'
