@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"
set "PROJECT=src\GlazeShell.App\GlazeShell.App.csproj"

echo Сборка GlazeShell (%CONFIG%)...
dotnet build "%PROJECT%" -c %CONFIG% -nologo

if errorlevel 1 (
  echo.
  echo Не удалось собрать GlazeShell. Проверьте вывод выше.
  pause
  exit /b 1
)

set "APP=src\GlazeShell.App\bin\x64\%CONFIG%\net10.0-windows10.0.19041.0\win-x64\GlazeShell.exe"

if not exist "%APP%" (
  for /f "usebackq delims=" %%P in (`dir /b /s "src\GlazeShell.App\bin\x64\%CONFIG%\GlazeShell.exe" 2^>nul`) do set "APP=%%P"
)

if not exist "%APP%" (
  echo.
  echo Сборка прошла, но исполняемый файл не найден.
  pause
  exit /b 1
)

start "" "%APP%"
exit /b 0
