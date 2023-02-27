@echo off
set BASE_DIR=%~dp0
set PLUGIN_DIR=%1
set PLUGIN_NAME=%2
set CONFIGURATION=%3

xcopy /Y /I %PLUGIN_DIR% %BASE_DIR%..\WindowTranslator\bin\%CONFIGURATION%\net6.0-windows10.0.22000.0\plugins\%PLUGIN_NAME%\