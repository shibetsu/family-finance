@echo off
set "root=%~dp0"
set "root=%root:~0,-1%"

echo Starting FinTool...

start "FinTool - API Server" cmd /k "cd /d "%root%" && dotnet run --project FinTool.Server"

start "FinTool - App" cmd /k "cd /d "%root%" && dotnet watch --project FinTool --launch-profile http"