@echo off
pushd "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-open-source-workflow.ps1" %*
popd
if %ERRORLEVEL% neq 0 (
    echo.
    echo Workflow failed. Run with --% to pass native arguments or open a terminal for details.
) else (
    echo.
    echo Open-source workflow complete.
)
pause
