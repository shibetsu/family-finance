@echo off
set "root=%~dp0"
set "root=%root:~0,-1%"

echo Starting FinTool...
echo App will be available at http://localhost:5111

start "FinTool" cmd /k "cd /d "%root%" && dotnet run --project FinTool.Server --launch-profile http"
