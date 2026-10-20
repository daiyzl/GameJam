$ErrorActionPreference = 'Stop'
$stageRoot = Split-Path $PSScriptRoot -Parent
$managed = 'D:\Unity\Unity editor\2022.3.62f2c1\Editor\Data\Managed'
$compiler = 'C:\Program Files\dotnet\sdk\10.0.401\Roslyn\bincore\csc.dll'
$arguments = @('/nologo','/nostdlib+','/target:library','/langversion:9','/define:UNITY_EDITOR', ('/out:"' + (Join-Path $PSScriptRoot 'UnityPrototype.dll') + '"'))
$arguments += Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1' -Filter '*.dll' | ForEach-Object { '/r:"' + $_.FullName + '"' }
$arguments += Get-ChildItem -LiteralPath (Join-Path $managed 'UnityEngine') -Filter '*.dll' | ForEach-Object { '/r:"' + $_.FullName + '"' }
$arguments += Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Scripts') -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
$arguments += '"' + (Join-Path $stageRoot 'Editor\PrototypeEditorBridge.cs') + '"'
$response = Join-Path $PSScriptRoot 'unity-compile.rsp'
Set-Content -LiteralPath $response -Value $arguments -Encoding utf8
& dotnet $compiler /noconfig ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw 'Unity assembly compilation failed.' }
Write-Output 'PASS: complete runtime and editor sources compile against Unity 2022.3 assemblies'
