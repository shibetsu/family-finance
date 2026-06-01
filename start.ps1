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
    "Set-Location '$root'; `$host.UI.RawUI.WindowTitle = 'FinTool - App'; dotnet run --project FinTool --launch-profile http"
)

Write-Host "Waiting 8s for servers to start..." -ForegroundColor Gray
Start-Sleep -Seconds 8

Write-Host "Opening browser at http://localhost:5254" -ForegroundColor Green
Start-Process "http://localhost:5254"
