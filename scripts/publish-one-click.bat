@echo off
pushd "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-one-click.ps1" %*
popd
if %ERRORLEVEL% neq 0 (
    echo.
    echo Publish command failed. Run from an elevated prompt if needed.
) else (
    echo.
    echo Publish command completed.
)
pause
