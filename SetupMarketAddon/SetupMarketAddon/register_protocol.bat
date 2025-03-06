@echo off 
echo Registering setupmarket:// protocol... 
 
REM Content Manager path - try to find it automatically 
set CM_PATH="" 
 
REM Check if a custom path was provided 
if "%~1" NEQ "" ( 
    set CM_PATH="%~1" 
    goto REGISTER 
) 
 
REM Try common paths 
IF EXIST "%USERPROFILE%\Documents\Programs\Content Manager\Content Manager.exe" ( 
    set CM_PATH="%USERPROFILE%\Documents\Programs\Content Manager\Content Manager.exe" 
    echo Found Content Manager in user Documents\Programs. 
) ELSE IF EXIST "C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\Content Manager.exe" ( 
    set CM_PATH="C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\Content Manager.exe" 
    echo Found Content Manager in Steam directory. 
) ELSE IF EXIST "%USERPROFILE%\Documents\Assetto Corsa\Content Manager.exe" ( 
    set CM_PATH="%USERPROFILE%\Documents\Assetto Corsa\Content Manager.exe" 
    echo Found Content Manager in Assetto Corsa directory. 
) ELSE ( 
    echo Content Manager.exe not found in common locations. 
    echo Please specify the path manually: 
    echo register_protocol.bat "C:\path\to\Content Manager.exe" 
    exit /b 1 
) 
 
:REGISTER 
echo Content Manager path: %CM_PATH% 
 
REM Create registry entries 
echo Creating registry entries... 
reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /ve /t REG_SZ /d "URL:Setup Market Protocol" 
reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /v "URL Protocol" /t REG_SZ /d "" 
reg add "HKCU\SOFTWARE\Classes\setupmarket\shell\open\command" /f /ve /t REG_SZ /d "%CM_PATH% \"%1\"" 
 
echo. 
echo Protocol registration complete! 
echo. 
echo You can now click on setupmarket:// links in your browser. 
echo. 
pause 
