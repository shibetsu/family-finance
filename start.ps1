$root = $PSScriptRoot

Write-Host "Starting FinTool..." -ForegroundColor Cyan

# API server
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "Set-Location '$root'; `$host.UI.RawUI.WindowTitle = 'FinTool - API Server'; dotnet run --project FinTool.Server"
)

# Blazor app
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "Set-Location '$root'; `$host.UI.RawUI.WindowTitle = 'FinTool - App'; dotnet watch --project FinTool --launch-profile http"
)

Write-Host "Waiting 8s for servers to start..." -ForegroundColor Gray
