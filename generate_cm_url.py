import sys
import base64
import json
import re
import os
import requests

def download_setup(setup_id):
    """Download a setup file from Setup Market"""
    setup_url = f"https://setupmarket.net/wp-content/uploads/setup-files/setup_{setup_id}.ini"
    print(f"Downloading setup from: {setup_url}")
    
    try:
        response = requests.get(setup_url)
        if response.status_code != 200:
            print(f"Failed to download setup file. Status code: {response.status_code}")
            return None
            
        setup_content = response.text
        
        # Extract car ID from the ini file
        car_id = None
        car_match = re.search(r'\[CAR\]\s+MODEL=(.+)', setup_content)
        if car_match:
            car_id = car_match.group(1).strip()
        
        if not car_id:
            print("Could not determine car ID from setup file")
            return None
            
        print(f"Found car ID: {car_id}")
        
        return {
            'content': setup_content,
            'car_id': car_id,
            'setup_id': setup_id
        }
    except Exception as e:
        print(f"Error downloading setup: {e}")
        return None

def create_cm_url(setup_data):
    """Create a CM URL from setup data"""
    try:
        # Encode the setup content to base64
        setup_content_b64 = base64.b64encode(setup_data['content'].encode('utf-8')).decode('utf-8')

        # Create the shared entry data
        shared_data = {
            'n': f"Setup for {setup_data['car_id']}",  # name
            't': setup_data['car_id'],                 # target (car)
            'a': "SetupMarket.net",                    # author
            'i': f"setup_{setup_data['setup_id']}.ini", # id
            'e': 1,                                    # entry type (1 = CarSetup)
            'd': setup_content_b64                     # data
        }

        # Encode the shared data to JSON then to base64
        shared_json = json.dumps(shared_data)
        shared_b64 = base64.b64encode(shared_json.encode('utf-8')).decode('utf-8')

        # Create the acmanager URL
        cm_url = f"acmanager://shared?id={shared_b64}"
        return cm_url
    except Exception as e:
        print(f"Error creating CM URL: {e}")
        return None

def main():
    if len(sys.argv) != 2:
        print("Usage: python generate_cm_url.py <setup_id>")
        return
        
    setup_id = sys.argv[1]
    print(f"Processing setup ID: {setup_id}")
    
    setup_data = download_setup(setup_id)
    if setup_data:
        cm_url = create_cm_url(setup_data)
        if cm_url:
            print("\nGenerated Content Manager URL:")
            print(cm_url)
            
            # Write to a file for easier copying
            with open("cm_url.txt", "w") as f:
                f.write(cm_url)
            print("\nURL also saved to cm_url.txt")
            
            choice = input("\nDo you want to open this URL in Content Manager? (y/n): ")
            if choice.lower() == 'y':
                os.startfile(cm_url)
                print("URL opened in Content Manager")

if __name__ == "__main__":
    main() 