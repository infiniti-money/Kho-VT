@echo off
setlocal
if not exist "%~dp0publish-reconciled\HISA Quan Ly Kho.exe" (
  echo Chua co ban Windows. Hay chay build_windows.bat truoc.
  pause
  exit /b 1
)
start "" "%~dp0publish-reconciled\HISA Quan Ly Kho.exe"
