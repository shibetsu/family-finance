param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
# Use $PSScriptRoot so all paths are absolute regardless of the caller's working directory.
# [System.IO.Path]::GetFullPath uses .NET's Environment.CurrentDirectory, which PowerShell's
# Set-Location does not update — anchoring to $PSScriptRoot avoids that mismatch.
$OutDir      = Join-Path $PSScriptRoot "release"
$VersionFile = Join-Path $PSScriptRoot "version.txt"

# Auto-increment patch if no version supplied; explicit version updates the file.
if ($Version -eq "") {
    $current = if (Test-Path $VersionFile) { (Get-Content $VersionFile -Raw).Trim() } else { "1.0.0" }
    $parts   = $current.Split('.')
    $parts[2] = [int]$parts[2] + 1
    $Version  = $parts -join '.'
}
Set-Content $VersionFile $Version -NoNewline
Write-Host "Building release packages v$Version..."

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }

# Creates a ZIP with forward-slash entry paths (Compress-Archive uses backslashes, breaking Linux extraction).
function New-CrossPlatformZip {
    param([string]$SourceDir, [string]$DestZip)
    Add-Type -Assembly System.IO.Compression.FileSystem
    $absSource = [System.IO.Path]::GetFullPath($SourceDir)
    if (Test-Path $DestZip) { Remove-Item $DestZip -Force }
    $zipStream = [System.IO.File]::Open($DestZip, [System.IO.FileMode]::Create)
    $archive   = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem $absSource -Recurse -File | ForEach-Object {
            $entryName = $_.FullName.Substring($absSource.Length).TrimStart('\', '/').Replace('\', '/')
            # CreateEntryFromFile is an extension method; PowerShell 5.1 requires the static call form.
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    } finally {
        $archive.Dispose()
        $zipStream.Dispose()
    }
}

# Creates a tar.gz using a temporary .NET 8 project so we can set exact Unix permissions per entry.
# The server binary gets rwxr-xr-x (755); everything else gets rw-r--r-- (644).
function New-LinuxTarGz {
    param(
        [string]   $SourceDir,
        [string]   $DestTarGz,
        [string[]] $Executables = @('FinTool.Server')
    )
    $absSource = [System.IO.Path]::GetFullPath($SourceDir)
    $absDest   = [System.IO.Path]::GetFullPath($DestTarGz)

    $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid())
    New-Item -ItemType Directory -Path $tmpDir | Out-Null

    $csproj = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
'@

    $program = @'
using System.Formats.Tar;
using System.IO.Compression;

var source   = args[0];
var dest     = args[1];
var exeNames = args.Skip(2).ToHashSet(StringComparer.OrdinalIgnoreCase);

using var fs = File.Create(dest);
using var gz = new GZipStream(fs, CompressionLevel.Optimal);
using var tw = new TarWriter(gz, TarEntryFormat.Pax, leaveOpen: false);

foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
{
    var rel = Path.GetRelativePath(source, dir).Replace('\\', '/') + '/';
    tw.WriteEntry(new PaxTarEntry(TarEntryType.Directory, rel) { Mode = (UnixFileMode)0b111_101_101 });
}
foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
{
    var rel  = Path.GetRelativePath(source, file).Replace('\\', '/');
    var mode = exeNames.Contains(Path.GetFileName(file))
        ? (UnixFileMode)0b111_101_101   // rwxr-xr-x
        : (UnixFileMode)0b110_100_100;  // rw-r--r--
    var entry = new PaxTarEntry(TarEntryType.RegularFile, rel) { Mode = mode };
    using var data = File.OpenRead(file);
    entry.DataStream = data;
    tw.WriteEntry(entry);
}
'@

    try {
        Set-Content -Path (Join-Path $tmpDir 'CreateTar.csproj') -Value $csproj -Encoding utf8
        Set-Content -Path (Join-Path $tmpDir 'Program.cs')       -Value $program -Encoding utf8

        $runArgs = @('run', '--nologo', '--project', $tmpDir, '--') + @($absSource, $absDest) + $Executables
        & dotnet @runArgs
        if ($LASTEXITCODE -ne 0) { throw "tar.gz creation failed for $DestTarGz" }
    } finally {
        Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$rids = @(
    @{ Rid = "win-x64";    Linux = $false },
    @{ Rid = "linux-x64";  Linux = $true  },
    @{ Rid = "linux-arm64"; Linux = $true  }
)

foreach ($t in $rids) {
    $rid  = $t.Rid
    $dest = "$OutDir\$rid"
    Write-Host "`nPublishing $rid..."

    dotnet publish (Join-Path $PSScriptRoot "FinTool.Server") `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -o $dest

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid" }

    $base = "$OutDir\family-finance-$Version-$rid"

    $zip = "$base.zip"
    Write-Host "Zipping $zip..."
    New-CrossPlatformZip -SourceDir $dest -DestZip $zip
    Write-Host "Done: $zip"

    if ($t.Linux) {
        $tarGz = "$base.tar.gz"
        Write-Host "Creating $tarGz..."
        New-LinuxTarGz -SourceDir $dest -DestTarGz $tarGz
        Write-Host "Done: $tarGz"
    }
}

Write-Host "`nAll packages built in $OutDir"
