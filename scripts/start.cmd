@echo off
rem This script lives in scripts/, so the repo root is its parent directory.
for %%i in ("%~dp0..") do set "root=%%~fi"

echo Starting FinTool...
echo App will be available at http://localhost:5111

start "FinTool" cmd /k "cd /d "%root%" && dotnet run --project FinTool.Server --launch-profile http"
