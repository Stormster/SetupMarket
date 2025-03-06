# Setup Market Addon - Installation and Troubleshooting

## Overview

The Setup Market Addon allows Content Manager to handle `setupmarket://` links directly from your website. When a user clicks a link like `setupmarket://setup/47596`, Content Manager will automatically download and install the setup file.

## Files Included

- `SetupMarketAddon.zip` - The addon package for Content Manager
- `register_protocol_fixed.bat` - A standalone script to register the protocol manually
- `test_protocol.html` - A test page with sample links to verify the protocol works
- `direct_url.py` - A script to generate direct `acmanager://` URLs (alternative approach)

## Installation Instructions

1. **Install the Addon in Content Manager**:
   - Open Content Manager
   - Go to Settings
   - Select "EXTENSION TOOLS"
   - Under "Addons", click "Install..."
   - Select the `SetupMarketAddon.zip` file and click Open
   - The addon will be installed and activated automatically

2. **Verify Protocol Registration**:
   - After installing the addon, Content Manager should automatically register the `setupmarket://` protocol
   - Open `test_protocol.html` in your browser and click on one of the test links
   - Content Manager should open and attempt to download the setup file

## Troubleshooting

If the `setupmarket://` links don't work after installing the addon:

1. **Check Addon Settings**:
   - In Content Manager, go to Settings > EXTENSION TOOLS
   - Make sure the Setup Market addon is enabled
   - Check that the "auto_register_protocol" option is set to "True" in the addon settings

2. **Manually Register the Protocol**:
   - Run the included `register_protocol_fixed.bat` file
   - If Content Manager is installed in a non-standard location, run the script with the path:
     ```
     register_protocol_fixed.bat "C:\path\to\your\Content Manager.exe"
     ```

3. **Check Registry Entries**:
   - Open Registry Editor (regedit)
   - Navigate to `HKEY_CURRENT_USER\SOFTWARE\Classes\setupmarket`
   - Verify that the following entries exist:
     - Default value: "URL:Setup Market Protocol"
     - "URL Protocol" value: "" (empty string)
     - Command: `"C:\path\to\Content Manager.exe" "%1"`

## Alternative Approach: Direct acmanager:// URLs

If you continue to have issues with the `setupmarket://` protocol, you can use direct `acmanager://` URLs instead:

1. Use the `direct_url.py` script to generate URLs for specific setup files:
   ```
   python direct_url.py 47596
   ```

2. The script will generate a URL like:
   ```
   acmanager://shared?id=eyJuIjogIlNldHVwIGZvciBoazUxX3RyYWZmaWNfaG9uZGFfamF6eiIsICJ0IjogImhrNTFfdHJhZmZpY19ob25kYV9qYXp6IiwgImEiOiAiU2V0dXBNYXJrZXQubmV0IiwgImkiOiAic2V0dXBfNDc1OTYuaW5pIiwgImUiOiAxLCAiZCI6ICJXMEZDVTE...
   ```

3. You can use these URLs directly on your website as an alternative to the `setupmarket://` protocol.

## Website Integration

For your website, you have two options:

1. **Use setupmarket:// links** (recommended if protocol registration works):
   ```html
   <a href="setupmarket://setup/47596">Download Honda Jazz Setup</a>
   ```

2. **Use direct acmanager:// links** (as a fallback):
   ```html
   <a href="acmanager://shared?id=eyJuIjogIlNldHVwIGZvciBoazUxX3RyYWZmaWNfaG9uZGFfamF6eiIsICJ0IjogImhrNTFfdHJhZmZpY19ob25kYV9qYXp6IiwgImEiOiAiU2V0dXBNYXJrZXQubmV0IiwgImkiOiAic2V0dXBfNDc1OTYuaW5pIiwgImUiOiAxLCAiZCI6ICJXMEZDVTE...">Download Honda Jazz Setup</a>
   ```

You could also implement a fallback system that tries the `setupmarket://` protocol first, and if it fails, uses the direct `acmanager://` URL as a backup.

## Support

If you continue to have issues, please check the log files:
- Addon log: `%USERPROFILE%\Documents\Assetto Corsa\plugins\SetupMarketAddon\setup_market.log` 