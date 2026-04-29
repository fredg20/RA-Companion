@echo off
chcp 65001 >nul
setlocal

pushd "%~dp0" >nul
if errorlevel 1 (
  echo Impossible d'ouvrir le dossier du projet.
  exit /b 1
)

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
set "BUILD_EXIT_CODE=%ERRORLEVEL%"

popd >nul
exit /b %BUILD_EXIT_CODE%
