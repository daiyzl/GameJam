$ErrorActionPreference = 'Stop'
$stageRoot = Split-Path $PSScriptRoot -Parent
$arguments = @('/nologo','/nostdlib+','/target:exe','/langversion:9',('/out:"'+(Join-Path $PSScriptRoot 'Checks.dll')+'"'))
$arguments += Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\10.0.12\ref\net10.0' -Filter '*.dll' | ForEach-Object { '/r:"'+$_.FullName+'"' }
$arguments += @('V1State.cs','V1Ecology.cs','V1Hydrology.cs','V1Settlements.cs','V1Animals.cs','V1Level.cs','V1Terrain.cs','V1BirdMigration.cs','V1PhaseEvents.cs','V1Honors.cs','V1WorldGenerator.cs') | ForEach-Object { '"'+(Join-Path (Join-Path $stageRoot 'Scripts') $_)+'"' }
$arguments += '"'+(Join-Path $PSScriptRoot 'Program.cs')+'"'
$arguments | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'checks.rsp') -Encoding UTF8
& dotnet 'C:\Program Files\dotnet\sdk\10.0.401\Roslyn\bincore\csc.dll' /noconfig ('@'+(Join-Path $PSScriptRoot 'checks.rsp'))
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed' }
'{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.12"}}}' | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Checks.runtimeconfig.json') -Encoding UTF8
& dotnet (Join-Path $PSScriptRoot 'Checks.dll')
if ($LASTEXITCODE -ne 0) { throw 'V1 rule checks failed' }


