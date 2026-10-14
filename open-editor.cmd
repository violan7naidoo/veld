@echo off
setlocal
rem ---------------------------------------------------------------------------
rem  Veld: open the project in the Godot 4.7.2 .NET editor.
rem  Double-click this file in Windows File Explorer. Inside the editor press
rem  F5 (or the Play button, top right) to run the game.
rem ---------------------------------------------------------------------------
set "GODOT=C:\Users\Damian work\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
set "HERE=%~dp0"
set "HERE=%HERE:~0,-1%"
cd /d "%HERE%"

echo ===========================================================
echo  VELD  -  opening the Godot editor
echo ===========================================================
if not exist "%GODOT%" (
  echo Godot was not found at:
  echo   %GODOT%
  echo Open this file in Notepad and fix the GODOT= line.
  pause
  exit /b 1
)
echo The editor can take 20-30 seconds to appear the first time. Leave this window open.
"%GODOT%" --editor --path "%HERE%"
if errorlevel 1 (
  echo.
  echo Godot stopped with an error. See the messages above.
  pause
)
endlocal
