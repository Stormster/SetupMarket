@echo off
echo Creating the Setup Market Addon package...

REM Directory for the addon
set ADDON_DIR=SetupMarketAddon
set OUTPUT_ZIP=%ADDON_DIR%.zip

REM Clean up any previous files
if exist "%OUTPUT_ZIP%" del "%OUTPUT_ZIP%"

REM Create font placeholder directory
if not exist "%ADDON_DIR%\font" mkdir "%ADDON_DIR%\font"
echo This is a placeholder for font files > "%ADDON_DIR%\font\README.txt"

REM Make sure settings directory exists
if not exist "%ADDON_DIR%\settings" mkdir "%ADDON_DIR%\settings"

REM Create a helper script for manual protocol registration
echo @echo off > "%ADDON_DIR%\register_protocol.bat"
echo echo Registering setupmarket:// protocol... >> "%ADDON_DIR%\register_protocol.bat"
echo. >> "%ADDON_DIR%\register_protocol.bat"
echo REM Content Manager path - try to find it automatically >> "%ADDON_DIR%\register_protocol.bat"
echo set CM_PATH="" >> "%ADDON_DIR%\register_protocol.bat"
echo. >> "%ADDON_DIR%\register_protocol.bat"
echo REM Check if a custom path was provided >> "%ADDON_DIR%\register_protocol.bat"
echo if "%%~1" NEQ "" ( >> "%ADDON_DIR%\register_protocol.bat"
echo     set CM_PATH="%%~1" >> "%ADDON_DIR%\register_protocol.bat"
echo     goto REGISTER >> "%ADDON_DIR%\register_protocol.bat"
echo ) >> "%ADDON_DIR%\register_protocol.bat"
echo. >> "%ADDON_DIR%\register_protocol.bat"
echo REM Try common paths >> "%ADDON_DIR%\register_protocol.bat"
echo IF EXIST "%%USERPROFILE%%\Documents\Programs\Content Manager\Content Manager.exe" ( >> "%ADDON_DIR%\register_protocol.bat"
echo     set CM_PATH="%%USERPROFILE%%\Documents\Programs\Content Manager\Content Manager.exe" >> "%ADDON_DIR%\register_protocol.bat"
echo     echo Found Content Manager in user Documents\Programs. >> "%ADDON_DIR%\register_protocol.bat"
echo ) ELSE IF EXIST "C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\Content Manager.exe" ( >> "%ADDON_DIR%\register_protocol.bat"
echo     set CM_PATH="C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\Content Manager.exe" >> "%ADDON_DIR%\register_protocol.bat"
echo     echo Found Content Manager in Steam directory. >> "%ADDON_DIR%\register_protocol.bat"
echo ) ELSE IF EXIST "%%USERPROFILE%%\Documents\Assetto Corsa\Content Manager.exe" ( >> "%ADDON_DIR%\register_protocol.bat"
echo     set CM_PATH="%%USERPROFILE%%\Documents\Assetto Corsa\Content Manager.exe" >> "%ADDON_DIR%\register_protocol.bat"
echo     echo Found Content Manager in Assetto Corsa directory. >> "%ADDON_DIR%\register_protocol.bat"
echo ) ELSE ( >> "%ADDON_DIR%\register_protocol.bat"
echo     echo Content Manager.exe not found in common locations. >> "%ADDON_DIR%\register_protocol.bat"
echo     echo Please specify the path manually: >> "%ADDON_DIR%\register_protocol.bat"
echo     echo register_protocol.bat "C:\path\to\Content Manager.exe" >> "%ADDON_DIR%\register_protocol.bat"
echo     exit /b 1 >> "%ADDON_DIR%\register_protocol.bat"
echo ) >> "%ADDON_DIR%\register_protocol.bat"
echo. >> "%ADDON_DIR%\register_protocol.bat"
echo :REGISTER >> "%ADDON_DIR%\register_protocol.bat"
echo echo Content Manager path: %%CM_PATH%% >> "%ADDON_DIR%\register_protocol.bat"
echo. >> "%ADDON_DIR%\register_protocol.bat"
echo REM Create registry entries >> "%ADDON_DIR%\register_protocol.bat"
echo echo Creating registry entries... >> "%ADDON_DIR%\register_protocol.bat"
echo reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /ve /t REG_SZ /d "URL:Setup Market Protocol" >> "%ADDON_DIR%\register_protocol.bat"
echo reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /v "URL Protocol" /t REG_SZ /d "" >> "%ADDON_DIR%\register_protocol.bat"
echo reg add "HKCU\SOFTWARE\Classes\setupmarket\shell\open\command" /f /ve /t REG_SZ /d "%%CM_PATH%% \"%%1\"" >> "%ADDON_DIR%\register_protocol.bat"
echo. >> "%ADDON_DIR%\register_protocol.bat"
echo echo. >> "%ADDON_DIR%\register_protocol.bat"
echo echo Protocol registration complete! >> "%ADDON_DIR%\register_protocol.bat"
echo echo. >> "%ADDON_DIR%\register_protocol.bat"
echo echo You can now click on setupmarket:// links in your browser. >> "%ADDON_DIR%\register_protocol.bat"
echo echo. >> "%ADDON_DIR%\register_protocol.bat"
echo pause >> "%ADDON_DIR%\register_protocol.bat"

REM Create the README with installation instructions
echo Setup Market Addon for Content Manager > "%ADDON_DIR%\README.txt"
echo ------------------------------------ >> "%ADDON_DIR%\README.txt"
echo. >> "%ADDON_DIR%\README.txt"
echo This addon allows Content Manager to handle setupmarket:// links. >> "%ADDON_DIR%\README.txt"
echo. >> "%ADDON_DIR%\README.txt"
echo Installation: >> "%ADDON_DIR%\README.txt"
echo 1. In Content Manager, go to Settings >> "%ADDON_DIR%\README.txt"
echo 2. Select "EXTENSION TOOLS" >> "%ADDON_DIR%\README.txt"
echo 3. Under "Addons", click "Install..." >> "%ADDON_DIR%\README.txt"
echo 4. Select the SetupMarketAddon.zip file and click Open >> "%ADDON_DIR%\README.txt"
echo 5. The addon will be installed and activated automatically >> "%ADDON_DIR%\README.txt"
echo. >> "%ADDON_DIR%\README.txt"
echo If setupmarket:// links don't work after installing the addon: >> "%ADDON_DIR%\README.txt"
echo 1. Make sure the addon is enabled in Content Manager >> "%ADDON_DIR%\README.txt"
echo 2. Run the included register_protocol.bat file to manually register the protocol >> "%ADDON_DIR%\README.txt"
echo. >> "%ADDON_DIR%\README.txt"
echo For support, visit setupmarket.net >> "%ADDON_DIR%\README.txt"

REM Create the package
powershell Compress-Archive -Path "%ADDON_DIR%\*" -DestinationPath "%OUTPUT_ZIP%" -Force
if %ERRORLEVEL% NEQ 0 (
    echo Failed to create the package!
    exit /b 1
)

echo.
echo Package created successfully: %OUTPUT_ZIP%
echo.

REM Clean up any previous package files
if exist "SetupMarketAddon.zip" del /F /Q "SetupMarketAddon.zip"
if exist "temp" rmdir /S /Q "temp"

REM Create required directories
mkdir "temp\Setup Market Addon"
mkdir "temp\Setup Market Addon\apps\python\setup_market"
mkdir "temp\Setup Market Addon\apps\python\setup_market\settings"
mkdir "temp\Setup Market Addon\content\fonts"

REM Copy files to the temp directory
copy "setup_market.py" "temp\Setup Market Addon\apps\python\setup_market\" /Y
copy "settings\settings_defaults.ini" "temp\Setup Market Addon\apps\python\setup_market\settings\" /Y
copy "settings\settings.ini" "temp\Setup Market Addon\apps\python\setup_market\settings\" /Y
copy "README.txt" "temp\Setup Market Addon\" /Y

REM Create fonts directory with necessary files
echo Creating placeholder for font files...
copy "font\Retro_Gaming.ttf" "temp\Setup Market Addon\content\fonts\" /Y
copy "font\Retro_Gaming.png" "temp\Setup Market Addon\content\fonts\" /Y
copy "font\Retro_Gaming.txt" "temp\Setup Market Addon\content\fonts\" /Y

REM Create zip file
powershell -Command "Compress-Archive -Path 'temp\Setup Market Addon' -DestinationPath 'SetupMarketAddon.zip' -Force"

REM Clean up
rmdir /S /Q "temp"

echo Packaging complete. SetupMarketAddon.zip is ready for distribution. 