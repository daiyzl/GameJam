$ErrorActionPreference = 'Stop'
$scriptDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'Scripts'
$compiler = 'C:\Program Files\dotnet\sdk\10.0.401\Roslyn\bincore\csc.dll'
$arguments = @('/nologo', '/nostdlib+', '/target:exe', '/langversion:9', ('/out:"' + (Join-Path $PSScriptRoot 'Rules.dll') + '"'))
$arguments += Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\10.0.12\ref\net10.0' -Filter '*.dll' | ForEach-Object { '/r:"' + $_.FullName + '"' }
$arguments += @('BaseElement.cs','EnvironmentSnapshot.cs','FiveElementSimulation.cs','PrototypeLevels.cs','SimulationVerification.cs') | ForEach-Object { '"' + (Join-Path $scriptDirectory $_) + '"' }
$arguments += '"' + (Join-Path $PSScriptRoot 'Program.cs') + '"'
$response = Join-Path $PSScriptRoot 'rules.rsp'
Set-Content -LiteralPath $response -Value $arguments -Encoding utf8
& dotnet $compiler /noconfig ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Rules.runtimeconfig.json') -Value '{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.12"}}}' -Encoding utf8
& dotnet (Join-Path $PSScriptRoot 'Rules.dll')
if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
