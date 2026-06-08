# Downloads family-finance.db from a remote machine over SSH/SCP into ./db-backups/.
# Prompts for connection details every run, pre-filling defaults from sync-db.params.json
# (created/updated automatically) so repeat runs just need Enter to confirm.

$ErrorActionPreference = "Stop"

# This script lives in scripts/, so the repo root is one level up; params file and
# backups live at the repo root alongside the other generated/ignored directories.
$RepoRoot   = Split-Path -Parent $PSScriptRoot
$ParamsFile = Join-Path $RepoRoot "sync-db.params.json"
$BackupDir  = Join-Path $RepoRoot "db-backups"

$saved = if (Test-Path $ParamsFile) { Get-Content $ParamsFile -Raw | ConvertFrom-Json } else { $null }

function Read-WithDefault([string]$Prompt, [string]$Default) {
    $suffix = if ($Default) { " [$Default]" } else { "" }
    $value = Read-Host "$Prompt$suffix"
    if ([string]::IsNullOrWhiteSpace($value)) { return $Default }
    return $value
}

$RemoteIp     = Read-WithDefault "Remote host/IP" $saved.RemoteIp
$RemoteUser   = Read-WithDefault "Remote user" $saved.RemoteUser
$RemoteDbPath = Read-WithDefault "Remote family-finance.db path" $saved.RemoteDbPath

if (-not $RemoteIp -or -not $RemoteUser -or -not $RemoteDbPath) {
    throw "Remote host, user, and database path are all required."
}

# Persist the (possibly edited) values so the next run can default to them.
[PSCustomObject]@{
    RemoteIp     = $RemoteIp
    RemoteUser   = $RemoteUser
    RemoteDbPath = $RemoteDbPath
} | ConvertTo-Json | Set-Content $ParamsFile

New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$destFile  = Join-Path $BackupDir "family-finance_$timestamp.db"

Write-Host "Downloading $RemoteUser@${RemoteIp}:$RemoteDbPath -> $destFile"
scp "${RemoteUser}@${RemoteIp}:$RemoteDbPath" $destFile

Write-Host "Saved to $destFile"
