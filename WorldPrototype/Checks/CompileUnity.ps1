$ErrorActionPreference = 'Stop'
$stageRoot = Split-Path $PSScriptRoot -Parent
$compiler = 'C:\Program Files\dotnet\sdk\10.0.401\Roslyn\bincore\csc.dll'
$managed = 'D:\Unity\Unity editor\2022.3.62f2c1\Editor\Data\Managed\UnityEngine'
$arguments = @('/nologo','/nostdlib+','/target:library','/langversion:9','/define:UNITY_EDITOR', ('/out:"' + (Join-Path $PSScriptRoot 'WorldPrototype.dll') + '"'))
$arguments += Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1' -Filter '*.dll' | ForEach-Object { '/r:"' + $_.FullName + '"' }
$arguments += Get-ChildItem -LiteralPath $managed -Filter '*.dll' | ForEach-Object { '/r:"' + $_.FullName + '"' }
$arguments += Get-ChildItem -LiteralPath 'C:\Users\DLQian\GameJam\Assets\Scripts' -Filter '*.cs' | Where-Object Name -ne 'FiveElementSimulation.cs' | ForEach-Object { '"' + $_.FullName + '"' }
$arguments += '"' + (Join-Path $stageRoot 'FiveElementSimulation.cs') + '"'
$arguments += Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Scripts') -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
$arguments += Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Editor') -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
$response = Join-Path $PSScriptRoot 'compile.rsp'
Set-Content -LiteralPath $response -Value $arguments -Encoding utf8
& dotnet $compiler /noconfig ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw 'World compilation failed.' }
Write-Output 'PASS: world runtime and editor code compile against installed Unity 2022.3'
