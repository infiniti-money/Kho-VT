@echo off
setlocal
cd /d "%~dp0"
set "LOCAL_DOTNET=%TEMP%\hisa-dotnet-sdk-10.0.403\dotnet.exe"
set "LOCAL_SDKS=%TEMP%\hisa-dotnet-sdk-10.0.403\sdk\10.0.403\Sdks"
if exist "%LOCAL_DOTNET%" (
  "%LOCAL_DOTNET%" publish HisaWarehouse.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:MSBuildSDKsPath="%LOCAL_SDKS%" -o "%~dp0publish-reconciled" --nologo
) else (
  dotnet publish HisaWarehouse.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%~dp0publish-reconciled" --nologo
)
if errorlevel 1 (
  echo Khong the dong goi. Hay cai .NET 10 SDK va chay lai.
  pause
  exit /b 1
)
if exist "%LOCAL_DOTNET%" (
  "%LOCAL_DOTNET%" publish Updater\HisaUpdater.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:MSBuildSDKsPath="%LOCAL_SDKS%" -o "%~dp0updater-publish" --nologo
) else (
  dotnet publish Updater\HisaUpdater.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%~dp0updater-publish" --nologo
)
if errorlevel 1 (
  echo Khong dong goi duoc trinh cap nhat HISA.
  pause
  exit /b 1
)
copy /y "%~dp0updater-publish\HISA.Updater.exe" "%~dp0publish-reconciled\HISA.Updater.exe" >nul
if errorlevel 1 (
  echo Khong copy duoc trinh cap nhat HISA.
  pause
  exit /b 1
)
echo Da tao ung dung tai thu muc publish-reconciled.
pause
