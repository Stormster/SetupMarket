Setup Market Addon for Content Manager 
------------------------------------ 
 
This addon allows Content Manager to handle setupmarket:// links. 
 
Installation: 
1. In Content Manager, go to Settings 
2. Select "EXTENSION TOOLS" 
3. Under "Addons", click "Install..." 
4. Select the SetupMarketAddon.zip file and click Open 
5. The addon will be installed and activated automatically 
 
If setupmarket:// links don't work after installing the addon: 
1. Make sure the addon is enabled in Content Manager 
2. Run the included register_protocol.bat file to manually register the protocol 
3. Make sure Python is installed on your system (the protocol handler requires it)
4. Check logs at %USERPROFILE%\setupmarket_debug.log for errors

Troubleshooting:
- If Chrome shows "Open Content Manager?" prompt but nothing happens:
  This means the protocol is registered but Content Manager isn't properly receiving
  the setup information. Try running register_protocol_fixed.bat to fix this issue.

- If you have Content Manager open and the setup isn't being added:
  The addon might not be properly handling the URL. Try updating to the latest
  version of the addon.

- For debugging, check the log file at:
  %USERPROFILE%\setupmarket_debug.log

For support, visit setupmarket.net 
