$ErrorActionPreference = 'Stop'
$stageRoot = Split-Path $PSScriptRoot -Parent
$projectRoot = 'C:\Users\DLQian\GameJam'
$compiler = 'C:\Program Files\dotnet\sdk\10.0.401\Roslyn\bincore\csc.dll'
$arguments = @('/nologo','/nostdlib+','/target:exe','/langversion:9',('/out:"'+(Join-Path $PSScriptRoot 'Checks.dll')+'"'))
$arguments += Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\10.0.12\ref\net10.0' -Filter '*.dll' | ForEach-Object { '/r:"'+$_.FullName+'"' }
$arguments += @('BaseElement.cs','EnvironmentSnapshot.cs','FiveElementSimulation.cs') | ForEach-Object { '"'+(Join-Path (Join-Path $projectRoot 'Assets\Scripts') $_)+'"' }
$arguments += '"'+(Join-Path $stageRoot 'Scripts\SettlementSimulation.cs')+'"'
$arguments += '"'+(Join-Path $PSScriptRoot 'Program.cs')+'"'
$response = Join-Path $PSScriptRoot 'checks.rsp'
$arguments | Set-Content -LiteralPath $response -Encoding utf8
& dotnet $compiler /noconfig ('@'+$response)
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
'{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.12"}}}' | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Checks.runtimeconfig.json') -Encoding utf8
& dotnet (Join-Path $PSScriptRoot 'Checks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Settlement checks failed' }
