@echo off
rem Wrapper ne loi CET tren Windows 10 chua co ban cap nhat: csc.exe (apphost .NET 10) chet ngay khi khoi dong,
rem nen chay csc.dll qua dotnet.exe. Directory.Build.props o thu muc goc tro CscToolPath/CscToolExe toi file nay
rem va truyen NHNHATRO_DOTNET + NHNHATRO_CSC_DLL (theo SDK dang build) qua CscEnvironment.
setlocal
if "%NHNHATRO_DOTNET%"=="" if not "%DOTNET_HOST_PATH%"=="" set "NHNHATRO_DOTNET=%DOTNET_HOST_PATH%"
if "%NHNHATRO_DOTNET%"=="" set "NHNHATRO_DOTNET=C:\Program Files\dotnet\dotnet.exe"
rem Du phong khi khong duoc truyen bien: hard-code SDK 10.0.402
if "%NHNHATRO_CSC_DLL%"=="" set "NHNHATRO_CSC_DLL=C:\Program Files\dotnet\sdk\10.0.402\Roslyn\bincore\csc.dll"
"%NHNHATRO_DOTNET%" exec "%NHNHATRO_CSC_DLL%" %*
