param(
    [Parameter(Mandatory=$true)][string]$ManagedPath,
    [Parameter(Mandatory=$true)][string]$BepInExCorePath,
    [string]$CompilerPath = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
)
$ErrorActionPreference = 'Stop'
$outputBuild = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $outputBuild -Force | Out-Null
$argumentsBuild = @('/nologo','/target:library','/optimize+',
    ('/out:' + (Join-Path $outputBuild 'AshlandsFireGuard.dll')),
    ('/reference:' + (Join-Path $BepInExCorePath 'BepInEx.dll')),
    ('/reference:' + (Join-Path $BepInExCorePath '0Harmony.dll')),
    ('/reference:' + (Join-Path $ManagedPath 'assembly_valheim.dll')),
    ('/reference:' + (Join-Path $ManagedPath 'UnityEngine.CoreModule.dll')),
    ('/reference:' + (Join-Path $ManagedPath 'UnityEngine.dll')),
    ('/reference:' + (Join-Path $ManagedPath 'netstandard.dll')),
    (Join-Path $PSScriptRoot 'src\AshlandsFireGuard.cs'))
& $CompilerPath @argumentsBuild
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Get-Item -LiteralPath (Join-Path $outputBuild 'AshlandsFireGuard.dll') | Select-Object FullName,Length
