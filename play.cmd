@echo off
setlocal
rem ---------------------------------------------------------------------------
rem  Veld: build the C# code and run the game (no editor).
rem  Double-click this file in Windows File Explorer. This window stays open
rem  while the game runs and shows any errors. Quit the game with Esc > Quit.
rem ---------------------------------------------------------------------------
set "GODOT=C:\Users\Damian work\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
set "DOTNET=C:\Program Files\dotnet\dotnet.exe"
set "HERE=%~dp0"
set "HERE=%HERE:~0,-1%"
cd /d "%HERE%"

echo ===========================================================
echo  VELD  -  Cape Colony, 1901
echo ===========================================================
if not exist "%GODOT%" (
  echo Godot was not found at:
  echo   %GODOT%
  echo Open this file in Notepad and fix the GODOT= line.
  pause
  exit /b 1
)
if not exist "%DOTNET%" set "DOTNET=dotnet"

echo [1/2] Building the C# code (the first time can take a minute)...
"%DOTNET%" build -nologo -v q
if errorlevel 1 (
  echo.
  echo Build failed. The errors are above.
  pause
  exit /b 1
)

echo [2/2] Starting the game. Leave this window open; it closes when you quit the game.
"%GODOT%" --path "%HERE%"
if errorlevel 1 (
  echo.
  echo Godot stopped with an error. See the messages above.
  pause
)
endlocal
