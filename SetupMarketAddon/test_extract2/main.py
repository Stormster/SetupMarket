import os
import sys
import winreg
import json
import urllib.request
import tempfile
import traceback
from pathlib import Path
import ctypes
import re
import base64

ADDON_NAME = "Setup Market"
ADDON_VERSION = "1.0.0"
URL_SCHEME = "setupmarket"
URL_PROTOCOL = f"{URL_SCHEME}://"
WEBSITE_URL = "https://setupmarket.net"

# Setup Market specific paths and details
SETUP_FILES_PATH = f"{WEBSITE_URL}/wp-content/uploads/setup-files/"

# Content Manager paths
def get_cm_dir():
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def get_user_dir():
    return os.path.join(os.path.expanduser("~"), "Documents", "Assetto Corsa", "setups")

def log(message):
    """Log a message to a log file in the addon's directory"""
    log_file = os.path.join(os.path.dirname(os.path.abspath(__file__)), "setupmarket.log")
    with open(log_file, "a", encoding="utf-8") as f:
        f.write(f"{message}\n")

def register_url_protocol():
    """Register the setupmarket:// URL protocol with Windows"""
    try:
        # Get the absolute path to the executable
        cm_path = os.path.join(get_cm_dir(), "Content Manager.exe")
        
        # Open or create the registry key
        key_path = f"SOFTWARE\\Classes\\{URL_SCHEME}"
        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, key_path) as key:
            winreg.SetValue(key, "", winreg.REG_SZ, "URL:Setup Market Protocol")
            winreg.SetValueEx(key, "URL Protocol", 0, winreg.REG_SZ, "")
            
            # Set the command to run
            with winreg.CreateKey(key, "shell\\open\\command") as cmd_key:
                cmd_value = f'"{cm_path}" "%1"'
                winreg.SetValue(cmd_key, "", winreg.REG_SZ, cmd_value)
        
        log(f"Registered URL protocol {URL_SCHEME}://")
        return True
    except Exception as e:
        log(f"Failed to register URL protocol: {str(e)}")
        log(traceback.format_exc())
        return False

def download_setup_file(setup_id):
    """Download a setup file from Setup Market"""
    try:
        setup_url = f"{SETUP_FILES_PATH}setup_{setup_id}.ini"
        log(f"Downloading setup from: {setup_url}")
        
        # Download the setup file
        with urllib.request.urlopen(setup_url) as response:
            setup_content = response.read().decode('utf-8')
            
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
        
        return {
            'content': final_setup_content,
            'car_id': car_id,
            'track_id': track_id,
            'setup_id': setup_id
        }
        
    except Exception as e:
        log(f"Error downloading setup file: {str(e)}")
        log(traceback.format_exc())
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
        return None

def process_url(url):
    """Process a setupmarket:// URL"""
    try:
        log(f"Processing URL: {url}")
        
        # Parse the URL to extract setup ID
        if url.startswith(URL_PROTOCOL):
            url_path = url[len(URL_PROTOCOL):]
            
            # Parse the URL path to extract the setup ID
            # Expected format: setupmarket://setup/12345
            match = re.match(r'setup/(\d+)', url_path)
            if match:
                setup_id = match.group(1)
                log(f"Extracted setup ID: {setup_id}")
                
                # Download the setup file
                setup_data = download_setup_file(setup_id)
                if setup_data:
                    # Create the CM command
                    cm_command = create_cm_shared_command(setup_data)
                    if cm_command:
                        log(f"Created CM command: {cm_command}")
                        
                        # Execute the command by opening the URL
                        os.startfile(cm_command)
                        return True
            else:
                log(f"Could not extract setup ID from URL: {url}")
        else:
            log(f"URL does not start with expected protocol: {URL_PROTOCOL}")
        
        return False
    except Exception as e:
        log(f"Error processing URL: {str(e)}")
        log(traceback.format_exc())
        return False

def main():
    try:
        log(f"\n--- {ADDON_NAME} Addon v{ADDON_VERSION} ---")
        log(f"Args: {sys.argv}")
        
        # Register URL protocol on startup
        register_url_protocol()
        
        # If a URL was passed, process it
        if len(sys.argv) > 1:
            url = sys.argv[1]
            process_url(url)
            
    except Exception as e:
        log(f"Error in main function: {str(e)}")
        log(traceback.format_exc())

if __name__ == "__main__":
    main() 