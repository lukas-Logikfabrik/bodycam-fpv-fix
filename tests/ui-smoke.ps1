param([string]$UiOutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
if (-not $UiOutputDirectory) { $UiOutputDirectory = Join-Path $root 'build/ui-check' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$csc = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/Roslyn/csc.exe' | Select-Object -First 1
$fw = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$refs = 'mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Xml.dll','netstandard.dll' | ForEach-Object { "/reference:$(Join-Path $fw $_)" }
$sources = Get-ChildItem "$root/src" -Filter *.cs | ForEach-Object FullName
$resources = @()
foreach ($asset in @('room.png','BebasNeue-Regular.ttf')) { $resources += "/resource:$root/assets/$asset,BodycamFpvFix.$asset" }
$resources += "/resource:$root/assets/OFL-BebasNeue.txt,BodycamFpvFix.OFL.txt"
& $csc /nologo /target:exe /main:BodycamFpvFix.UiSmoke /langversion:7.3 "/out:$root/build/UiSmoke.exe" $refs "/reference:$root/build/Nefarius.ViGEm.Client.dll" $resources $sources "$PSScriptRoot/UiSmoke.cs"
if ($LASTEXITCODE -ne 0) { throw 'UI test compile failed' }
& "$root/build/UiSmoke.exe" $UiOutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'UI test failed' }
