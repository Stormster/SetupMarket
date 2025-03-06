import base64
import json

# The setup ID from the URL
setup_id = "47596"

# The car data from the example INI file
car_id = "hk51_traffic_honda_jazz"

# This is a simplified version of the INI file content based on the example
setup_content = """[ABS] VALUE=1 
[CAMBER_LF] VALUE=-31 
[CAMBER_LR] VALUE=-27 
[CAMBER_RF] VALUE=-36 
[CAMBER_RR] VALUE=-28 
[CAR] MODEL=hk51_traffic_honda_jazz 
[FUEL] VALUE=34 
[PRESSURE_LF] VALUE=27 
[PRESSURE_LR] VALUE=27 
[PRESSURE_RF] VALUE=27 
[PRESSURE_RR] VALUE=27 
[TOE_OUT_LF] VALUE=2 
[TOE_OUT_LR] VALUE=2 
[TOE_OUT_RF] VALUE=5 
[TOE_OUT_RR] VALUE=3 
[TRACTION_CONTROL] VALUE=0 
[TYRES] VALUE=1 
[__EXT_PATCH] VERSION=0.2.7-preview1"""

print(f"Creating URL for setup ID: {setup_id}, car: {car_id}")

# Encode the setup content to base64
setup_content_b64 = base64.b64encode(setup_content.encode('utf-8')).decode('utf-8')

# Create the shared entry data
shared_data = {
    'n': f"Setup for {car_id}",  # name
    't': car_id,                 # target (car)
    'a': "SetupMarket.net",      # author
    'i': f"setup_{setup_id}.ini", # id
    'e': 1,                      # entry type (1 = CarSetup)
    'd': setup_content_b64       # data
}

# Encode the shared data to JSON then to base64
shared_json = json.dumps(shared_data)
shared_b64 = base64.b64encode(shared_json.encode('utf-8')).decode('utf-8')

# Create the acmanager URL
cm_url = f"acmanager://shared?id={shared_b64}"

print("\nGenerated Content Manager URL:")
print(cm_url)

# Write to a file for easier copying
with open("cm_url.txt", "w") as f:
    f.write(cm_url)
print("\nURL also saved to cm_url.txt") 