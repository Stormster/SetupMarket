@echo off
echo Registering setupmarket:// protocol...

REM Content Manager path - found automatically
set CM_PATH=C:\Users\Storm\Documents\Programs\Content Manager\Content Manager.exe

echo Content Manager path: "%CM_PATH%"

REM Create registry entries
echo Creating registry entries...
reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /ve /t REG_SZ /d "URL:Setup Market Protocol"
reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /v "URL Protocol" /t REG_SZ /d ""
reg add "HKCU\SOFTWARE\Classes\setupmarket\shell\open\command" /f /ve /t REG_SZ /d "\"%CM_PATH%\" \"%%1\""

echo.
echo Protocol registration complete!
echo.
echo You can now test the protocol by opening test_protocol.html in your browser
echo and clicking on one of the setupmarket:// links.
echo. 