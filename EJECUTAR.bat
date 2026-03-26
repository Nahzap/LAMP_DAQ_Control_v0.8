@echo off
cd /d "%~dp0"
echo =======================================================
echo LAMP DAQ Control v0.8 - INICIANDO MODO CONSOLA (Legacy)
echo =======================================================
echo.
echo Lanzando binario Release optimizado en modo terminal...
start "" "bin\Release\LAMP_DAQ_Control_v0.8.exe" -console
