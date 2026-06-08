# Deploys a published linux-x64 release to a remote machine running the app under systemd.
# Prompts for connection/install details every run, pre-filling defaults from
# deploy-remote.params.json (created/updated automatically) so repeat runs just need Enter.
#
# Flow: upload the archive via scp, then over a single ssh session: stop the service,
# extract the archive over the install directory, and start the service back up.
# Run .\publish.ps1 -Version <x.y.z> first to produce the archive this script looks for.

$ErrorActionPreference = "Stop"

$ParamsFile  = Join-Path $PSScriptRoot "deploy-remote.params.json"
$ReleaseDir  = Join-Path $PSScriptRoot "release"
$VersionFile = Join-Path $PSScriptRoot "version.txt"

$saved = if (Test-Path $ParamsFile) { Get-Content $ParamsFile -Raw | ConvertFrom-Json } else { $null }
$latestVersion = if (Test-Path $VersionFile) { (Get-Content $VersionFile -Raw).Trim() } else { $saved.Version }

function Read-WithDefault([string]$Prompt, [string]$Default) {
    $suffix = if ($Default) { " [$Default]" } else { "" }
    $value = Read-Host "$Prompt$suffix"
    if ([string]::IsNullOrWhiteSpace($value)) { return $Default }
    return $value
}

function Read-YesNo([string]$Prompt, [bool]$Default) {
    $defaultStr = if ($Default) { "y" } else { "n" }
    $value = Read-Host "$Prompt (y/n) [$defaultStr]"
    if ([string]::IsNullOrWhiteSpace($value)) { return $Default }
    return $value.Trim().ToLower() -eq "y"
}

$RemoteIp         = Read-WithDefault "Remote host/IP" $saved.RemoteIp
$RemoteUser       = Read-WithDefault "Remote user" $saved.RemoteUser
$RemoteInstallDir = Read-WithDefault "Remote install directory" $saved.RemoteInstallDir
$ServiceName      = Read-WithDefault "Systemd service name" $saved.ServiceName
$UseSudo          = Read-YesNo "Run remote systemctl/extract commands with sudo?" ([bool]$saved.UseSudo)
$Version          = Read-WithDefault "Version to deploy" $latestVersion

if (-not $RemoteIp -or -not $RemoteUser -or -not $RemoteInstallDir -or -not $ServiceName -or -not $Version) {
    throw "Host, user, install directory, service name, and version are all required."
}

# .tar.gz packages only — systemd implies a Linux/macOS remote, so the win-x64 .zip never applies.
$packages = @(Get-ChildItem $ReleaseDir -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "family-finance-$Version-*" -and $_.Extension -eq '.gz' })
if (-not $packages) {
    throw "No release packages found for version $Version in $ReleaseDir`nRun .\publish.ps1 -Version $Version first."
}

Write-Host "`nPackages available for $Version`:"
for ($i = 0; $i -lt $packages.Count; $i++) {
    Write-Host "  [$($i + 1)] $($packages[$i].Name)"
}
$selection = Read-Host "Which package to send? [1]"
if ([string]::IsNullOrWhiteSpace($selection)) { $selection = "1" }
$selectedIndex = [int]$selection - 1
if ($selectedIndex -lt 0 -or $selectedIndex -ge $packages.Count) {
    throw "Invalid selection: $selection"
}
$archivePath = $packages[$selectedIndex].FullName
$archiveName = $packages[$selectedIndex].Name

# Persist the (possibly edited) values so the next run can default to them.
[PSCustomObject]@{
    RemoteIp         = $RemoteIp
    RemoteUser       = $RemoteUser
    RemoteInstallDir = $RemoteInstallDir
    ServiceName      = $ServiceName
    UseSudo          = $UseSudo
    Version          = $Version
} | ConvertTo-Json | Set-Content $ParamsFile

$sudo          = if ($UseSudo) { "sudo " } else { "" }
$remoteStaging = "/tmp/$archiveName"

Write-Host ""
Write-Host "About to deploy:"
Write-Host "  $archivePath"
Write-Host "  -> ${RemoteUser}@${RemoteIp}:$RemoteInstallDir (service: $ServiceName, sudo: $UseSudo)"
Read-Host "Press Enter to continue, or Ctrl+C to abort"

Write-Host "`nUploading archive..."
scp $archivePath "${RemoteUser}@${RemoteIp}:$remoteStaging"

$remoteCommand = "${sudo}systemctl stop $ServiceName && " +
                 "${sudo}tar -xzf $remoteStaging -C $RemoteInstallDir && " +
                 "rm -f $remoteStaging && " +
                 "${sudo}systemctl start $ServiceName && " +
                 "${sudo}systemctl status $ServiceName --no-pager"

Write-Host "`nRunning remote update (stop -> extract -> start)..."
# -t forces a pseudo-terminal so 'sudo' has somewhere to prompt for a password.
ssh -t "${RemoteUser}@${RemoteIp}" $remoteCommand

Write-Host "`nDeployment complete."
