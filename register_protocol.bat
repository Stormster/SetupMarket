@echo off
echo Registering setupmarket:// protocol...

REM Check if a custom path was provided
set CM_PATH=""
if "%~1" NEQ "" (
    set CM_PATH="%~1"
    goto REGISTER
)

REM Attempt to find Content Manager path
IF EXIST "C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\Content Manager.exe" (
    set CM_PATH="C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\Content Manager.exe"
) ELSE IF EXIST "%USERPROFILE%\Documents\Assetto Corsa\Content Manager.exe" (
    set CM_PATH="%USERPROFILE%\Documents\Assetto Corsa\Content Manager.exe"
)

:REGISTER
echo Content Manager path: %CM_PATH%

REM Create registry entries
reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /ve /t REG_SZ /d "URL:Setup Market Protocol"
reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /v "URL Protocol" /t REG_SZ /d ""
reg add "HKCU\SOFTWARE\Classes\setupmarket\shell\open\command" /f /ve /t REG_SZ /d "%CM_PATH% \"%%1\""

echo.
echo Protocol registration complete. Try clicking a setupmarket:// link now.
echo.
echo If it still doesn't work, please specify your Content Manager path:
echo register_protocol.bat "C:\path\to\your\Content Manager.exe"
echo.
pause 