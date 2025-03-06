@echo off
echo Registering setupmarket:// protocol...

REM Check if a custom path was provided
set CM_PATH=""
if "%~1" NEQ "" (
    set CM_PATH="%~1"
    goto REGISTER
)

REM Attempt to find Content Manager path
IF EXIST "C:\Users\Storm\Documents\Programs\Content Manager\Content Manager.exe" (
    set CM_PATH="C:\Users\Storm\Documents\Programs\Content Manager\Content Manager.exe"
    echo Found Content Manager in custom directory.
) ELSE IF EXIST "C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\Content Manager.exe" (
    set CM_PATH="C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\Content Manager.exe"
    echo Found Content Manager in Steam directory.
) ELSE IF EXIST "%USERPROFILE%\Documents\Assetto Corsa\Content Manager.exe" (
    set CM_PATH="%USERPROFILE%\Documents\Assetto Corsa\Content Manager.exe"
    echo Found Content Manager in user documents.
) ELSE (
    echo Content Manager.exe not found in common locations.
    echo You may need to run this script with a path parameter:
    echo register_protocol_updated.bat "C:\path\to\your\Content Manager.exe"
    exit /b 1
)

:REGISTER
echo Content Manager path: %CM_PATH%

REM Create registry entries
echo Creating registry entries...
reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /ve /t REG_SZ /d "URL:Setup Market Protocol"
reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /v "URL Protocol" /t REG_SZ /d ""

REM Need to properly escape the quotes in the command value
setlocal EnableDelayedExpansion
set COMMAND_VALUE=!CM_PATH! "%%1"
reg add "HKCU\SOFTWARE\Classes\setupmarket\shell\open\command" /f /ve /t REG_SZ /d "!COMMAND_VALUE!"
endlocal

echo.
echo Protocol registration complete!
echo.
echo You can now test the protocol by opening test_protocol.html in your browser
echo and clicking on one of the setupmarket:// links.
echo.
echo If it still doesn't work, please run this script with your Content Manager path:
echo register_protocol_updated.bat "C:\path\to\your\Content Manager.exe"
echo. 