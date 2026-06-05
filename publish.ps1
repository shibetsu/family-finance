param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$OutDir = ".\release"

Write-Host "Building release packages v$Version..."

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }

$rids = @(
    @{ Rid = "win-x64";   Ext = ".exe" },
    @{ Rid = "linux-x64"; Ext = ""     }
)

foreach ($t in $rids) {
    $rid  = $t.Rid
    $dest = "$OutDir\$rid"
    Write-Host "`nPublishing $rid..."

    dotnet publish FinTool.Server `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -o $dest

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid" }

    $zip = "$OutDir\family-finance-$Version-$rid.zip"
    Write-Host "Zipping $zip..."
    Compress-Archive -Path "$dest\*" -DestinationPath $zip -Force

    Write-Host "Done: $zip"
}

Write-Host "`nAll packages built in $OutDir"
