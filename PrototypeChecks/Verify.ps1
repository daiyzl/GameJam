$ErrorActionPreference = 'Stop'
$checkDirectory = $PSScriptRoot
$sourceDirectory = Join-Path (Split-Path $checkDirectory -Parent) 'PrototypeScripts'
$compilerPath = 'C:\Program Files\dotnet\sdk\10.0.401\Roslyn\bincore\csc.dll'
$unityManaged = 'D:\Unity\Unity editor\2022.3.62f2c1\Editor\Data\Managed'
$sources = @('BaseElement.cs', 'EnvironmentSnapshot.cs', 'GameRuler.cs') | ForEach-Object {
    '"' + (Join-Path $sourceDirectory $_) + '"'
}

$unityArguments = @('/nologo', '/nostdlib+', '/target:library', '/langversion:9', '/define:UNITY_EDITOR',
    ('/out:"' + (Join-Path $checkDirectory 'UnityCompile.dll') + '"'))
$unityArguments += Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1' -Filter '*.dll' | ForEach-Object { '/r:"' + $_.FullName + '"' }
$unityArguments += @('UnityEngine\UnityEngine.dll', 'UnityEngine\UnityEngine.CoreModule.dll', 'UnityEngine\UnityEngine.IMGUIModule.dll', 'UnityEditor.dll') | ForEach-Object { '/r:"' + (Join-Path $unityManaged $_) + '"' }
$unityArguments += $sources
$unityResponse = Join-Path $checkDirectory 'unity-compile.rsp'
Set-Content -LiteralPath $unityResponse -Value $unityArguments -Encoding utf8
& dotnet $compilerPath /noconfig ('@' + $unityResponse)
if ($LASTEXITCODE -ne 0) { throw 'Unity assembly compilation failed.' }
Write-Output 'PASS: compilation against Unity 2022.3 assemblies (including UNITY_EDITOR)'

$checkArguments = @('/nologo', '/nostdlib+', '/target:exe', '/langversion:9',
    ('/out:"' + (Join-Path $checkDirectory 'RulesChecks.dll') + '"'))
$checkArguments += Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\10.0.12\ref\net10.0' -Filter '*.dll' | ForEach-Object { '/r:"' + $_.FullName + '"' }
$checkArguments += $sources
$checkArguments += @('UnityStubs.cs', 'RulesChecks.cs') | ForEach-Object { '"' + (Join-Path $checkDirectory $_) + '"' }
$checkResponse = Join-Path $checkDirectory 'rules-checks.rsp'
Set-Content -LiteralPath $checkResponse -Value $checkArguments -Encoding utf8
& dotnet $compilerPath /noconfig ('@' + $checkResponse)
if ($LASTEXITCODE -ne 0) { throw 'Rule check compilation failed.' }
Set-Content -LiteralPath (Join-Path $checkDirectory 'RulesChecks.runtimeconfig.json') -Value '{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.12"}}}' -Encoding utf8
& dotnet (Join-Path $checkDirectory 'RulesChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Rule checks failed.' }
