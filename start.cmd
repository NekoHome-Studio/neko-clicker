@echo off
rem start.cmd -- double-click entry point for NekoClicker.
rem
rem Why a .cmd wrapper: PowerShell's execution policy blocks .ps1 files by default
rem (a real trap this repo already hit), and a .cmd is double-clickable, so the
rem player never has to know about policies, verbs, or PowerShell syntax at all.
rem
rem Usage:
rem   start.cmd                the browser side (default): web host + opens the browser
rem   start.cmd web company    same, with a specific content pack
rem   start.cmd play lab       terminal UI + a specific pack (does NOT start the web host)
rem   start.cmd list           show pack ids
rem   start.cmd web -NoBrowser start / reuse the host without opening a browser
rem
rem Why the browser is the default: the terminal and the web host write the SAME save file
rem (saves\<pack>.json), and each rewrites it whole every 60s from its own memory, so running
rem both silently rolls one side's progress back. tools\start.ps1 has the file/line evidence.
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
