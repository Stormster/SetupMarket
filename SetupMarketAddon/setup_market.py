##############################################################
# Setup Market Addon
# ver. 1.0 / March 2025
# Made for setupmarket.net
##############################################################

import os
import sys
import winreg
import json
import configparser
import requests
import tempfile
import traceback
import base64
import re
import ac
import acsys

# Constants
ADDON_NAME = "Setup Market"
ADDON_VERSION = "1.0.0"
URL_SCHEME = "setupmarket"
URL_PROTOCOL = f"{URL_SCHEME}://"
WEBSITE_URL = "https://setupmarket.net"
SETUP_FILES_PATH = f"{WEBSITE_URL}/wp-content/uploads/setup-files/"

# Setup app labels
app_window = 0
info_label = 0
status_label = 0

# Settings paths
settings_default_ini_path = os.path.join(os.path.dirname(os.path.realpath(__file__)), "settings", "settings_defaults.ini")
settings_ini_path = os.path.join(os.path.dirname(os.path.realpath(__file__)), "settings", "settings.ini")
log_file_path = os.path.join(os.path.dirname(os.path.realpath(__file__)), "setup_market.log")

# Load settings
config = configparser.ConfigParser()
if not os.path.isfile(settings_ini_path):
    with open(settings_ini_path, "w", encoding="utf-8") as ini:
        ini.write("")

# Load settings
config.read(settings_default_ini_path)
config.read(settings_ini_path)

def log(message):
    """Log a message to a log file in the addon's directory"""
    with open(log_file_path, "a", encoding="utf-8") as f:
        f.write(f"{message}\n")

def get_cm_dir():
    """Get the Content Manager directory"""
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def get_cm_exe_path():
    """Get the Content Manager executable path with proper fallbacks"""
    # Start with the parent directory of the addon
    cm_path = os.path.join(get_cm_dir(), "Content Manager.exe")
    if os.path.exists(cm_path):
        return cm_path
    
    # Try common installation paths
    common_paths = [
        os.path.join(os.path.expanduser("~"), "Documents", "Programs", "Content Manager", "Content Manager.exe"),
        os.path.join("C:", os.sep, "Program Files (x86)", "Steam", "steamapps", "common", "assettocorsa", "Content Manager.exe"),
        os.path.join(os.path.expanduser("~"), "Documents", "Assetto Corsa", "Content Manager.exe")
    ]
    
    for path in common_paths:
        if os.path.exists(path):
            return path
    
    # Return the default path even if it doesn't exist
    return cm_path

def get_user_dir():
    """Get the user's Assetto Corsa setups directory"""
    return os.path.join(os.path.expanduser("~"), "Documents", "Assetto Corsa", "setups")

def register_url_protocol():
    """Register the setupmarket:// URL protocol with Windows"""
    try:
        # Get the absolute path to the executable
        cm_path = get_cm_exe_path()
        
        log(f"Using Content Manager path: {cm_path}")
        
        if not os.path.exists(cm_path):
            log(f"Content Manager executable not found at: {cm_path}")
            update_status("Content Manager not found!")
            return False
        
        # Open or create the registry key
        key_path = f"SOFTWARE\\Classes\\{URL_SCHEME}"
        try:
            with winreg.CreateKey(winreg.HKEY_CURRENT_USER, key_path) as key:
                winreg.SetValue(key, "", winreg.REG_SZ, "URL:Setup Market Protocol")
                winreg.SetValueEx(key, "URL Protocol", 0, winreg.REG_SZ, "")
                
                # Set the command to run
                with winreg.CreateKey(key, "shell\\open\\command") as cmd_key:
                    cmd_value = f'"{cm_path}" "%1"'
                    winreg.SetValue(cmd_key, "", winreg.REG_SZ, cmd_value)
            
            log(f"Registered URL protocol {URL_SCHEME}://")
            update_status("URL Protocol registered successfully")
            return True
        except PermissionError:
            log("Permission error when registering URL protocol - attempting backup method")
            
            # Try a more direct registry update approach as backup
            try:
                os.system(f'reg add "HKCU\\SOFTWARE\\Classes\\{URL_SCHEME}" /f /ve /t REG_SZ /d "URL:Setup Market Protocol"')
                os.system(f'reg add "HKCU\\SOFTWARE\\Classes\\{URL_SCHEME}" /f /v "URL Protocol" /t REG_SZ /d ""')
                os.system(f'reg add "HKCU\\SOFTWARE\\Classes\\{URL_SCHEME}\\shell\\open\\command" /f /ve /t REG_SZ /d "\\"{cm_path}\\" \\"%1\\"" ')
                
                log(f"Registered URL protocol {URL_SCHEME}:// using backup method")
                update_status("URL Protocol registered successfully (backup method)")
                return True
            except Exception as backup_error:
                log(f"Backup method also failed: {str(backup_error)}")
                update_status("Protocol registration failed completely")
                return False
    except Exception as e:
        log(f"Failed to register URL protocol: {str(e)}")
        log(traceback.format_exc())
        update_status("Failed to register URL protocol")
        return False

def download_setup_file(setup_id):
    """Download a setup file from Setup Market"""
    try:
        setup_url = f"{SETUP_FILES_PATH}setup_{setup_id}.ini"
        log(f"Downloading setup from: {setup_url}")
        update_status(f"Downloading setup ID: {setup_id}...")
        
        # Download the setup file using requests
        response = requests.get(setup_url)
        if response.status_code != 200:
            log(f"Failed to download setup file. Status code: {response.status_code}")
            update_status(f"Download failed: HTTP {response.status_code}")
            return None
            
        setup_content = response.text
            
        # Extract car ID and track ID from the ini file
        car_id = None
        track_id = None
        
        # Look for metadata
        metadata = {}
        for line in setup_content.split('\n'):
            if line.startswith(';CM_META:'):
                key_value = line[9:].split(':', 1)
                if len(key_value) == 2:
                    metadata[key_value[0]] = key_value[1]
        
        # Try to find car ID in metadata or in the content
        if 'car' in metadata:
            car_id = metadata['car']
        else:
            car_match = re.search(r'\[CAR\]\s+MODEL=(.+)', setup_content)
            if car_match:
                car_id = car_match.group(1).strip()
        
        # Try to find track ID in metadata
        if 'track' in metadata:
            track_id = metadata['track']
        
        if not car_id:
            log("Could not determine car ID from setup file")
            update_status("Error: Could not determine car ID")
            return None
        
        # Prepare metadata for Content Manager
        setup_metadata = {
            'car': car_id
        }
        
        if track_id:
            setup_metadata['track'] = track_id
        
        # Add metadata to the setup file
        metadata_content = ""
        for key, value in setup_metadata.items():
            encoded_value = base64.b64encode(value.encode('utf-8')).decode('utf-8')
            metadata_content += f";CM_META:{key}:{encoded_value}\n"
        
        # Combine metadata with the setup content
        final_setup_content = metadata_content + setup_content
        
        update_status(f"Setup downloaded for {car_id}")
        return {
            'content': final_setup_content,
            'car_id': car_id,
            'track_id': track_id,
            'setup_id': setup_id
        }
        
    except Exception as e:
        log(f"Error downloading setup file: {str(e)}")
        log(traceback.format_exc())
        update_status("Error downloading setup file")
        return None

def create_cm_shared_command(setup_data):
    """Create a command to open Content Manager with the setup"""
    try:
        # Encode the setup content to base64
        setup_content_b64 = base64.b64encode(setup_data['content'].encode('utf-8')).decode('utf-8')
        
        # Create the shared entry data
        shared_data = {
            'n': f"Setup for {setup_data['car_id']}",  # name
            't': setup_data['car_id'],  # target (car)
            'a': "SetupMarket.net",     # author
            'i': f"setup_{setup_data['setup_id']}.ini",  # id
            'e': 1,  # entry type (1 = CarSetup)
            'd': setup_content_b64  # data
        }
        
        # Encode the shared data to JSON then to base64
        shared_json = json.dumps(shared_data)
        shared_b64 = base64.b64encode(shared_json.encode('utf-8')).decode('utf-8')
        
        # Create the command
        cmd = f"acmanager://shared?id={shared_b64}"
        return cmd
        
    except Exception as e:
        log(f"Error creating CM shared command: {str(e)}")
        log(traceback.format_exc())
        update_status("Error creating CM command")
        return None

def process_url(url):
    """Process a setupmarket:// URL"""
    try:
        log(f"Processing URL: {url}")
        update_status(f"Processing URL: {url}")
        
        # Parse the URL to extract setup ID
        if url.startswith(URL_PROTOCOL):
            url_path = url[len(URL_PROTOCOL):]
            
            # Parse the URL path to extract the setup ID
            # Expected format: setupmarket://setup/12345
            match = re.match(r'setup/(\d+)', url_path)
            if match:
                setup_id = match.group(1)
                log(f"Extracted setup ID: {setup_id}")
                update_status(f"Found setup ID: {setup_id}")
                
                # Download the setup file
                setup_data = download_setup_file(setup_id)
                if setup_data:
                    # Create the CM command
                    cm_command = create_cm_shared_command(setup_data)
                    if cm_command:
                        log(f"Created CM command: {cm_command}")
                        update_status("Opening setup in Content Manager...")
                        
                        # Execute the command by opening the URL
                        os.startfile(cm_command)
                        update_status("Setup loaded successfully!")
                        return True
            else:
                log(f"Could not extract setup ID from URL: {url}")
                update_status("Invalid setup URL format")
        else:
            log(f"URL does not start with expected protocol: {URL_PROTOCOL}")
            update_status(f"Invalid URL protocol: {url}")
        
        return False
    except Exception as e:
        log(f"Error processing URL: {str(e)}")
        log(traceback.format_exc())
        update_status("Error processing URL")
        return False

def update_status(message):
    """Update the status label in the app"""
    global status_label
    if status_label:
        ac.setText(status_label, message)

def acMain(ac_version):
    """Initialize the app"""
    global app_window, info_label, status_label
    
    log(f"\n--- {ADDON_NAME} Addon v{ADDON_VERSION} ---")
    
    # Create the app window
    app_window = ac.newApp(ADDON_NAME)
    ac.setTitle(app_window, "")
    ac.setIconPosition(app_window, -10000, -10000)
    
    # Set window size and appearance
    width = int(config.get('INTERFACE', 'backgroundwidth', fallback="500"))
    height = 100
    ac.setSize(app_window, width, height)
    ac.setBackgroundOpacity(app_window, float(config.get('INTERFACE', 'backgroundopacity', fallback="0.5")))
    ac.drawBackground(app_window, 1)
    ac.drawBorder(app_window, 0)
    
    # Create labels
    info_label = ac.addLabel(app_window, f"{ADDON_NAME} v{ADDON_VERSION}")
    ac.setPosition(info_label, 10, 10)
    ac.setFontSize(info_label, 18)
    
    status_label = ac.addLabel(app_window, "Initializing...")
    ac.setPosition(status_label, 10, 40)
    ac.setFontSize(status_label, 14)
    
    # Register URL protocol
    register_url_protocol()
    update_status("Ready to download setups from setupmarket.net")
    
    return "Setup Market Addon"

def acUpdate(deltaT):
    """Update function called by AC"""
    # This function currently has no update logic
    pass

def acShutdown():
    """Cleanup when app is closed"""
    log("Shutting down Setup Market Addon") 