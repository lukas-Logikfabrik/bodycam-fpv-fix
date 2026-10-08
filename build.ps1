<#
  Builds dist\BodycamFpvFix.exe from the sources in src\.
  Needs Windows with .NET Framework 4.8 and the Roslyn C# compiler from Visual Studio 2019+ or the VS Build Tools.
  The GitHub release workflow runs exactly this script.
#>
param([string]$Version = '0.0.0')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root 'build'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $build, $dist | Out-Null

# 1. ViGEm client library (MIT, by Nefarius), pinned to one version and checksum
$pkgVersion = '1.21.256'
$pkgSha256 = 'FF460031B33E0882CD1166FE2D072D0FE97578480C61ED85DCCFA6C930FDB55E'
$pkg = Join-Path $build "Nefarius.ViGEm.Client.$pkgVersion.nupkg"
if (-not (Test-Path $pkg)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest "https://www.nuget.org/api/v2/package/Nefarius.ViGEm.Client/$pkgVersion" -OutFile $pkg -UseBasicParsing
}
if ((Get-FileHash $pkg -Algorithm SHA256).Hash -ne $pkgSha256) { Remove-Item $pkg; throw "Checksum mismatch for $pkg" }
$lib = Join-Path $build 'Nefarius.ViGEm.Client.dll'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($pkg)
try {
    $entry = $zip.Entries | Where-Object FullName -eq 'lib/netstandard2.0/Nefarius.ViGEm.Client.dll'
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $lib, $true)
} finally { $zip.Dispose() }

# 2. Compiler
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$csc = $null
if (Test-Path $vswhere) {
    $csc = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
}
if (-not $csc) { throw 'Roslyn csc.exe not found. Install Visual Studio or the Visual Studio Build Tools.' }
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'

# 3. Version info
$numeric = if ($Version -match '^v?(\d+)\.(\d+)\.(\d+)') { "$($Matches[1]).$($Matches[2]).$($Matches[3])" } else { '0.0.0' }
$info = Join-Path $build 'AssemblyInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyTitle("Bodycam FPV Fix")]
[assembly: AssemblyProduct("Bodycam FPV Fix")]
[assembly: AssemblyDescription("Use a USB RC radio (BETAFPV, EdgeTX, ...) as an Xbox controller for the Bodycam FPV drone")]
[assembly: AssemblyCopyright("MIT License")]
[assembly: AssemblyVersion("$numeric.0")]
[assembly: AssemblyFileVersion("$numeric.0")]
[assembly: AssemblyInformationalVersion("$Version")]
"@ | Set-Content $info -Encoding UTF8

# 4. Compile
$out = Join-Path $dist 'BodycamFpvFix.exe'
$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName
$refs = 'mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Runtime.Serialization.dll', 'System.Xml.dll', 'netstandard.dll' |
    ForEach-Object { "/reference:$(Join-Path $fw $_)" }
& $csc /nologo /noconfig /nostdlib+ /target:winexe /platform:anycpu /optimize+ /deterministic+ /debug- /langversion:7.3 `
    "/pathmap:$root=." "/out:$out" "/win32manifest:$(Join-Path $root 'app.manifest')" `
    $refs "/reference:$lib" "/resource:$lib,Nefarius.ViGEm.Client.dll" $sources $info
if ($LASTEXITCODE -ne 0) { throw "Compiler failed with exit code $LASTEXITCODE" }

# 5. Checksum next to the exe
$hash = (Get-FileHash $out -Algorithm SHA256).Hash
"$hash  BodycamFpvFix.exe" | Set-Content (Join-Path $dist 'BodycamFpvFix.exe.sha256') -Encoding Ascii
Write-Host "Built $out"
Write-Host "SHA256 $hash"
