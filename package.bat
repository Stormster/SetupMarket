@echo off
echo Packaging SetupMarketAddon...

REM Delete the old zip file if it exists
if exist SetupMarketAddon.zip del SetupMarketAddon.zip

REM Create the zip file using PowerShell
powershell -Command "Compress-Archive -Path SetupMarketAddon -DestinationPath SetupMarketAddon.zip -Force"

echo.
echo Package complete: SetupMarketAddon.zip
echo.
echo Next steps:
echo 1. Install the addon in Content Manager: Settings -^> EXTENSION TOOLS -^> Addons -^> Install...
echo 2. Run "register_protocol_fixed.bat" inside the addon folder
echo 3. Open test_protocol.html in a web browser and click a test link
echo.
pause 