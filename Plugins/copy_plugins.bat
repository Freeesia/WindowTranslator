@echo off
set BASE_DIR=%~dp0
set PLUGIN_DIR=%1
set PLUGIN_NAME=%2
set CONFIGURATION=%3

xcopy /S /Y /I %PLUGIN_DIR% %BASE_DIR%..\WindowTranslator\bin\%CONFIGURATION%\plugins\%PLUGIN_NAME%\