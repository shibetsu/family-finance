@echo off
title FinTool Launcher
cd /d "%~dp0"

echo Starting FinTool...
echo.

start "FinTool - API Server" cmd /k "title FinTool - API Server && dotnet run --project FinTool.Server"
start "FinTool - App"        cmd /k "title FinTool - App        && dotnet run --project FinTool --launch-profile http"

echo Waiting for servers to start...
timeout /t 8 /nobreak > nul

echo Opening browser...
start "" "http://localhost:5254"

exit
