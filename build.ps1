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

# 2. ILRepack (Apache-2.0), a build tool that merges the ViGEm library into the exe; pinned to one version and checksum
$repackVersion = '2.0.48'
$repackSha256 = '799017B829A6ED69FAC0D4FC0A874A4A6A9951F46D73A3CCE20BA016796B949F'
$repackPkg = Join-Path $build "ILRepack.$repackVersion.nupkg"
if (-not (Test-Path $repackPkg)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest "https://www.nuget.org/api/v2/package/ILRepack/$repackVersion" -OutFile $repackPkg -UseBasicParsing
}
if ((Get-FileHash $repackPkg -Algorithm SHA256).Hash -ne $repackSha256) { Remove-Item $repackPkg; throw "Checksum mismatch for $repackPkg" }
$repack = Join-Path $build 'ILRepack.exe'
$zip = [IO.Compression.ZipFile]::OpenRead($repackPkg)
try {
    $entry = $zip.Entries | Where-Object FullName -eq 'tools/ILRepack.exe'
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $repack, $true)
} finally { $zip.Dispose() }

# 3. Compiler
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$csc = $null
if (Test-Path $vswhere) {
    $csc = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
}
if (-not $csc) { throw 'Roslyn csc.exe not found. Install Visual Studio or the Visual Studio Build Tools.' }
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'

# 4. Version info
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

# 5. Compile
$exe = Join-Path $build 'BodycamFpvFix.exe'
$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName
$refs = 'mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Runtime.Serialization.dll', 'System.Xml.dll', 'netstandard.dll' |
    ForEach-Object { "/reference:$(Join-Path $fw $_)" }
& $csc /nologo /noconfig /nostdlib+ /target:winexe /platform:anycpu /optimize+ /deterministic+ /debug- /langversion:7.3 `
    "/pathmap:$root=." "/out:$exe" "/win32manifest:$(Join-Path $root 'app.manifest')" `
    $refs "/reference:$lib" $sources $info
if ($LASTEXITCODE -ne 0) { throw "Compiler failed with exit code $LASTEXITCODE" }

# 6. Merge the ViGEm library into the exe, so the program stays a single file without loading code at runtime
$out = Join-Path $dist 'BodycamFpvFix.exe'
& $repack /ndebug "/lib:$fw" "/targetplatform:v4,$fw" "/out:$out" $exe $lib
if ($LASTEXITCODE -ne 0) { throw "ILRepack failed with exit code $LASTEXITCODE" }

# 7. Checksum next to the exe
$hash = (Get-FileHash $out -Algorithm SHA256).Hash
"$hash  BodycamFpvFix.exe" | Set-Content (Join-Path $dist 'BodycamFpvFix.exe.sha256') -Encoding Ascii

# 8. Release zip: the exe, the official signed ViGEmBus installer (BSD 3-Clause), licenses and a short readme.
#    The program itself never downloads anything; the driver installer travels in the zip.
$driverName = 'ViGEmBus_1.22.0_x64_x86_arm64.exe'
$driverSha256 = '89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A'
$driver = Join-Path $build $driverName
if (-not (Test-Path $driver)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/$driverName" -OutFile $driver -UseBasicParsing
}
if ((Get-FileHash $driver -Algorithm SHA256).Hash -ne $driverSha256) { Remove-Item $driver; throw "Checksum mismatch for $driver" }
$stage = Join-Path $build 'zip'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'driver') | Out-Null
Copy-Item $out (Join-Path $stage 'BodycamFpvFix.exe')
Copy-Item $driver (Join-Path $stage "driver\$driverName")
Copy-Item (Join-Path $root 'packaging\README.txt') (Join-Path $stage 'README.txt')
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'LICENSE.txt')
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') (Join-Path $stage 'THIRD-PARTY-NOTICES.txt')
$zipName = "BodycamFpvFix-$Version.zip"
$zipOut = Join-Path $dist $zipName
if (Test-Path $zipOut) { Remove-Item $zipOut }
Get-ChildItem $dist -Filter 'BodycamFpvFix-*.zip*' | Remove-Item
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zipOut, [IO.Compression.CompressionLevel]::Optimal, $false)
$zipHash = (Get-FileHash $zipOut -Algorithm SHA256).Hash
"$zipHash  $zipName" | Set-Content "$zipOut.sha256" -Encoding Ascii

Write-Host "Built $out"
Write-Host "SHA256 $hash"
Write-Host "Built $zipOut"
Write-Host "SHA256 $zipHash"
