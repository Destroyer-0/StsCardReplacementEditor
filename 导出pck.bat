@echo off
setlocal
rem 一键导出：先导入资源（新增/改动了 png 时必须跑），再打包 pck。导出前请关闭游戏。
rem dll 由 dotnet build 生成，本脚本只负责 pck。

set "GODOT=E:\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe"

pushd "%~dp0"

if not exist "%GODOT%" (
    echo [FAILED] Godot not found: %GODOT%
    echo Edit GODOT in this bat, then retry.
    popd
    if /i not "%~1"=="-nopause" pause
    exit /b 1
)

rem 游戏目录与 mod 名都从 csproj 取，避免两处各写一份
set "CSPROJ="
for %%f in ("%~dp0*.csproj") do set "CSPROJ=%%~ff"
if not defined CSPROJ (
    echo [FAILED] no .csproj found next to this bat
    popd
    if /i not "%~1"=="-nopause" pause
    exit /b 1
)

for %%f in ("%CSPROJ%") do set "MODNAME=%%~nf"

set "STS2DIR="
for /f "tokens=3 delims=<>" %%a in ('findstr /r /c:"<Sts2Dir>" "%CSPROJ%"') do set "STS2DIR=%%a"
if not defined STS2DIR (
    echo [FAILED] Sts2Dir not found in %CSPROJ%
    popd
    if /i not "%~1"=="-nopause" pause
    exit /b 1
)

set "OUT=%STS2DIR%\mods\%MODNAME%\%MODNAME%.pck"
echo Godot  : %GODOT%
echo Output : %OUT%
echo.

"%GODOT%" --headless --path "%CD%" --import
if errorlevel 1 (
    echo [FAILED] import failed
    popd
    if /i not "%~1"=="-nopause" pause
    exit /b 1
)

"%GODOT%" --headless --path "%CD%" --export-pack "Windows Desktop" "%OUT%"
set "RC=%ERRORLEVEL%"
popd

if not "%RC%"=="0" (
    echo [FAILED] export failed, exit code %RC%
    if /i not "%~1"=="-nopause" pause
    exit /b %RC%
)

echo [OK] %OUT%
if /i not "%~1"=="-nopause" pause
