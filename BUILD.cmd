@echo off
echo ==========================================
echo Compilando LAMP_DAQ_Control_v0.8
echo ==========================================

REM Buscar MSBuild
for /f "tokens=*" %%i in ('"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2^>nul') do set MSBUILD=%%i

if "%MSBUILD%"=="" (
    echo ERROR: No se encontro MSBuild
    echo Por favor abre el proyecto en Visual Studio y compila desde alli
    pause
    exit /b 1
)

echo Usando: %MSBUILD%
echo.

REM Restaurar paquetes NuGet
"%MSBUILD%" "%~dp0LAMP_DAQ_Control_v0.8.csproj" /t:Restore
echo.

REM Limpiar
"%MSBUILD%" "%~dp0LAMP_DAQ_Control_v0.8.csproj" /t:Clean /p:Configuration=Release
echo.

REM Compilar
"%MSBUILD%" "%~dp0LAMP_DAQ_Control_v0.8.csproj" /t:Build /p:Configuration=Release /v:minimal

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ==========================================
    echo COMPILACION EXITOSA (RELEASE MODE)
    echo ==========================================
    echo Ejecutable: %~dp0bin\Release\LAMP_DAQ_Control_v0.8.exe
) else (
    echo.
    echo ==========================================
    echo ERROR EN COMPILACION
    echo ==========================================
)

pause
