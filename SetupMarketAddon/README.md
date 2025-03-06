# Setup Market Addon for Content Manager

This addon adds support for the `setupmarket://` URL protocol to Content Manager, allowing you to easily add car setups from [SetupMarket.net](https://setupmarket.net) directly to your Content Manager installation.

## Installation

1. Download the addon ZIP file
2. Open Content Manager
3. Drag and drop the ZIP file onto Content Manager to install it
4. The addon will automatically register the `setupmarket://` URL protocol with Windows

## Usage

When browsing setups on [SetupMarket.net](https://setupmarket.net), you can click on the "Add to CM" button, which will open Content Manager and prompt you to install the setup.

## How It Works

The addon registers the `setupmarket://` URL protocol with Windows, which allows web links to open Content Manager and pass parameters to it. When you click on an "Add to CM" link on SetupMarket.net, the addon:

1. Downloads the setup file from SetupMarket.net
2. Extracts relevant information like car ID and track ID
3. Formats the setup data to be compatible with Content Manager
4. Opens Content Manager with the setup data, prompting you to save it

## Website Integration

To add "Add to CM" buttons to your website, create links in the following format:

```html
<a href="setupmarket://setup/12345">Add to Content Manager</a>
```

Where `12345` is the ID of the setup file.

## Troubleshooting

If you encounter any issues:

1. Check the log file located in the addon's directory: `setupmarket.log`
2. Make sure Content Manager is installed correctly
3. Try reinstalling the addon

## License

This addon is provided as-is, with no warranty or guarantee of functionality. 