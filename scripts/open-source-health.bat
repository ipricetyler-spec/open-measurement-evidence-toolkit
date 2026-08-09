@echo off
pushd "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0open-source-health.ps1"
popd
if %ERRORLEVEL% neq 0 (
    echo.
    echo Health check failed.
) else (
    echo.
    echo Health check complete.
)
pause
