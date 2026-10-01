@echo off
rem start.cmd -- double-click entry point for NekoClicker.
rem
rem Why a .cmd wrapper: PowerShell's execution policy blocks .ps1 files by default
rem (a real trap this repo already hit), and a .cmd is double-clickable, so the
rem player never has to know about policies, verbs, or PowerShell syntax at all.
rem
rem Usage:
rem   start.cmd                terminal UI, default content pack
rem   start.cmd web            browser side (opens the browser for you)
rem   start.cmd play lab       terminal + a specific pack
rem   start.cmd list           show pack ids
rem
rem Everything that needs explaining is explained by tools\start.ps1; this file
rem only exists to bypass the execution policy and keep the window open.

setlocal
set "HERE=%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%tools\start.ps1" %*

rem If the game exited (or failed) keep the window so a double-clicker can read it.
if errorlevel 1 (
    echo.
    echo [start.cmd] exited with code %errorlevel%.
    pause
)
endlocal
