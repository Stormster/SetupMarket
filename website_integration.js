/**
 * Website Integration for Setup Market
 * This script helps create direct download links for Content Manager setups
 */

// Function to create a direct acmanager:// URL from a setup ID
function createDirectLink(setupId) {
    // Since we can't process the setup file contents in JavaScript on the client-side
    // due to CORS restrictions, we'll create a setupmarket:// URL instead
    return `setupmarket://setup/${setupId}`;
}

// Function to create a fallback link for users without the addon
function createFallbackLink(setupId) {
    return `https://setupmarket.net/wp-content/uploads/setup-files/setup_${setupId}.ini`;
}

// Function to create both buttons (for with and without the addon)
function createSetupButtons(setupId, containerSelector) {
    const container = document.querySelector(containerSelector);
    if (!container) return;
    
    // Create the direct CM button (for users with the addon)
    const cmButton = document.createElement('a');
    cmButton.href = createDirectLink(setupId);
    cmButton.className = 'cm-download-button';
    cmButton.innerHTML = '<img src="cm-icon.png" alt="CM"> Install with Content Manager';
    cmButton.addEventListener('click', function(e) {
        // Track that the user clicked the CM button
        if (typeof gtag !== 'undefined') {
            gtag('event', 'click_cm_button', {
                'setup_id': setupId
            });
        }
    });
    
    // Create the fallback button (direct download)
    const downloadButton = document.createElement('a');
    downloadButton.href = createFallbackLink(setupId);
    downloadButton.className = 'direct-download-button';
    downloadButton.innerHTML = 'Download Setup File';
    downloadButton.download = `setup_${setupId}.ini`;
    
    // Add buttons to the container
    container.appendChild(cmButton);
    container.appendChild(document.createElement('br'));
    container.appendChild(downloadButton);
}

// Add this to your website's initialization code
document.addEventListener('DOMContentLoaded', function() {
    // Example: Create setup buttons for setup ID 47596
    // Replace with your actual setup ID and container selector
    createSetupButtons('47596', '#setup-download-container');
    
    // For dynamic creation based on page content
    document.querySelectorAll('[data-setup-id]').forEach(function(el) {
        const setupId = el.getAttribute('data-setup-id');
        createSetupButtons(setupId, `#setup-container-${setupId}`);
    });
});

/**
 * Example HTML usage:
 * 
 * <div id="setup-download-container"></div>
 * 
 * OR for multiple setups:
 * 
 * <div data-setup-id="47596" id="setup-container-47596"></div>
 * <div data-setup-id="12345" id="setup-container-12345"></div>
 */

/**
 * Example CSS styles:
 * 
 * .cm-download-button {
 *     display: inline-block;
 *     background-color: #3498db;
 *     color: white;
 *     padding: 10px 15px;
 *     border-radius: 4px;
 *     text-decoration: none;
 *     margin-bottom: 10px;
 *     font-weight: bold;
 * }
 * 
 * .cm-download-button img {
 *     height: 20px;
 *     vertical-align: middle;
 *     margin-right: 5px;
 * }
 * 
 * .direct-download-button {
 *     display: inline-block;
 *     background-color: #2ecc71;
 *     color: white;
 *     padding: 8px 12px;
 *     border-radius: 4px;
 *     text-decoration: none;
 *     font-size: 0.9em;
 * }
 */ 