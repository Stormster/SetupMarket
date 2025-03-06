import base64

import json
import os
import re

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
        
        # For website integration, also return the formatted URL
        return cmd
    except Exception as e:
        print(f"Error creating CM command: {e}")
        return None

def process_url(url):
    """Process a setupmarket:// URL"""
    try:
        log(f"Processing URL: {url}")
        update_status(f"Processing URL: {url}")

        # Direct conversion for setupmarket IDs to acmanager URLs
        # This is a simplified approach to directly use acmanager:// URLs
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
        print(f"Error processing URL: {e}")
        return False 