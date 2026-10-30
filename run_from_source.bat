@echo off
setlocal
cd /d "%~dp0"
set "LOCAL_DOTNET=%TEMP%\hisa-dotnet-sdk-10.0.403\dotnet.exe"
set "LOCAL_SDKS=%TEMP%\hisa-dotnet-sdk-10.0.403\sdk\10.0.403\Sdks"
if exist "%LOCAL_DOTNET%" (
  "%LOCAL_DOTNET%" run --project HisaWarehouse.csproj -p:MSBuildSDKsPath="%LOCAL_SDKS%"
) else (
  dotnet run --project HisaWarehouse.csproj
)
if errorlevel 1 pause
