@echo off
echo Registering setupmarket:// protocol...

REM Content Manager path - found automatically
set CM_PATH=C:\Users\Storm\Documents\Programs\Content Manager\Content Manager.exe

echo Content Manager path: "%CM_PATH%"

REM Create the wrapper Python script
echo Creating Python wrapper script...
set WRAPPER_PATH=%TEMP%\setupmarket_handler.py

echo import os, sys, re > "%WRAPPER_PATH%"
echo import subprocess >> "%WRAPPER_PATH%"
echo. >> "%WRAPPER_PATH%"
echo # Log file for debugging >> "%WRAPPER_PATH%"
echo log_file = os.path.join(os.path.expanduser('~'), 'setupmarket_debug.log') >> "%WRAPPER_PATH%"
echo. >> "%WRAPPER_PATH%"
echo def log(message): >> "%WRAPPER_PATH%"
echo     with open(log_file, 'a') as f: >> "%WRAPPER_PATH%"
echo         f.write(f"{message}\n") >> "%WRAPPER_PATH%"
echo. >> "%WRAPPER_PATH%"
echo if len(sys.argv) > 1: >> "%WRAPPER_PATH%"
echo     try: >> "%WRAPPER_PATH%"
echo         # Log the arguments received >> "%WRAPPER_PATH%"
echo         log(f"\n--- New URL request ---") >> "%WRAPPER_PATH%"
echo         log(f"Args: {sys.argv}") >> "%WRAPPER_PATH%"
echo. >> "%WRAPPER_PATH%"
echo         # Get the setupmarket:// URL from arguments >> "%WRAPPER_PATH%"
echo         url = sys.argv[1] >> "%WRAPPER_PATH%"
echo         log(f"Processing URL: {url}") >> "%WRAPPER_PATH%"
echo. >> "%WRAPPER_PATH%"
echo         # Extract setup ID from the URL >> "%WRAPPER_PATH%"
echo         if url.startswith('setupmarket://'): >> "%WRAPPER_PATH%"
echo             url_path = url[len('setupmarket://'):] >> "%WRAPPER_PATH%"
echo             match = re.match(r'setup/(\d+)', url_path) >> "%WRAPPER_PATH%"
echo. >> "%WRAPPER_PATH%"
echo             if match: >> "%WRAPPER_PATH%"
echo                 setup_id = match.group(1) >> "%WRAPPER_PATH%"
echo                 log(f"Found setup ID: {setup_id}") >> "%WRAPPER_PATH%"
echo. >> "%WRAPPER_PATH%"
echo                 # Create acmanager:// URL with the setup ID >> "%WRAPPER_PATH%"
echo                 cmd = f"acmanager://setup/{setup_id}" >> "%WRAPPER_PATH%"
echo                 log(f"Created CM command: {cmd}") >> "%WRAPPER_PATH%"
echo. >> "%WRAPPER_PATH%"
echo                 # Launch Content Manager with the command >> "%WRAPPER_PATH%"
echo                 log(f"Launching: {cmd}") >> "%WRAPPER_PATH%"
echo                 os.startfile(cmd) >> "%WRAPPER_PATH%"
echo             else: >> "%WRAPPER_PATH%"
echo                 log(f"Could not extract setup ID from URL: {url}") >> "%WRAPPER_PATH%"
echo         else: >> "%WRAPPER_PATH%"
echo             log(f"URL does not start with setupmarket:// protocol: {url}") >> "%WRAPPER_PATH%"
echo     except Exception as e: >> "%WRAPPER_PATH%"
echo         log(f"Error processing URL: {str(e)}") >> "%WRAPPER_PATH%"
echo         import traceback >> "%WRAPPER_PATH%"
echo         log(traceback.format_exc()) >> "%WRAPPER_PATH%"

REM Create registry entries
echo Creating registry entries...
reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /ve /t REG_SZ /d "URL:Setup Market Protocol"
reg add "HKCU\SOFTWARE\Classes\setupmarket" /f /v "URL Protocol" /t REG_SZ /d ""
reg add "HKCU\SOFTWARE\Classes\setupmarket\shell\open\command" /f /ve /t REG_SZ /d "python \"%WRAPPER_PATH%\" \"%%1\""

echo.
echo Protocol registration complete!
echo.
echo The wrapper script will attempt to:
echo 1. Parse the setupmarket:// URL to extract the setup ID
echo 2. Convert it to an acmanager:// URL that Content Manager understands
echo 3. Log all actions to %USERPROFILE%\setupmarket_debug.log
echo.
echo You can now test the protocol in your browser.
echo.
pause 