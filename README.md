# Jimaku.cc Subtitle Plugin for Emby

This Emby plugin integrates Jimaku.cc as a subtitle provider, allowing users to fetch subtitles for their media library directly from Jimaku.cc.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Installation](#installation)
   - [Download and Copy the DLL](#download-and-copy-the-dll)
   - [Signing Up for a Jimaku.cc Account and Getting an API Key](#signing-up-for-a-jimakucc-account-and-getting-an-api-key)
   - [Configuring the Plugin in Emby](#configuring-the-plugin-in-emby)
3. [Usage](#usage)
4. [Images](#images)
5. [FAQ](#faq)
6. [Support](#support)

---

## Prerequisites

- An [Emby](https://emby.media/) server installation.
- A Jimaku.cc account with an API key.
- The Jimaku.cc plugin DLL file for Emby.

## Installation

### 1. Download and Copy the DLL

1. **Download the Plugin DLL**: You can download the compiled plugin DLL from the release page of this project.
   
2. **Copy the DLL to the Emby Plugins Folder**:
   - Navigate to your Emby installation folder.
   - Locate the `plugins` folder.
   - Copy the downloaded DLL file into this folder.
   
   Example path on a typical Emby server installation:
   - **Windows**: `C:\Users\[YourUsername]\AppData\Roaming\Emby-Server\plugins\`
   - **Linux**: `/var/lib/emby/plugins/`
   - **macOS**: `/Users/[YourUsername]/.config/emby-server/plugins/`

3. **Restart the Emby Server**: After copying the plugin DLL file, restart the Emby server to load the new plugin.

### 2. Signing Up for a Jimaku.cc Account and Getting an API Key

1. **Go to Jimaku.cc**: Visit [Jimaku.cc](https://jimaku.cc) and sign up for an account.
   
2. **Generate Your API Key**:
   - After signing in, go to your account settings page.
   - Navigate to the "API" section.
   - Click the button to generate a new API key.
   
3. **Copy Your API Key**: Make sure to copy the key, as it will be used in the next steps to configure the plugin.

### 3. Configuring the Plugin in Emby

1. **Open Emby**: Log into your Emby dashboard.
   
2. **Navigate to Plugins Settings**:
   - Go to `Settings > Plugins`.
   - You should see the Jimaku.cc plugin listed under installed plugins.
   
3. **Add Your API Key**:
   - Click on the Jimaku.cc plugin to open the configuration page.
   - You will see a field where you can paste your Jimaku.cc API key.
   - Paste your key and save the configuration.
   
4. **Enable Jimaku.cc Subtitles**: Ensure that Jimaku.cc is enabled as a subtitle provider in your Emby settings.

---

## Usage

Once you’ve installed and configured the Jimaku.cc plugin, subtitles will automatically be fetched for your media content when available. You can also manually search for subtitles through the Emby interface.

To manually fetch subtitles:
1. Open your media in Emby.
2. Select `Subtitles` from the playback menu.
3. Click `Search for Subtitles` and Jimaku.cc will be used to retrieve them.

---

### 1. Plugin Installation Location

![image](https://github.com/user-attachments/assets/3d780b7f-828d-43d6-a938-25e14da6bd45)

---

### 2. API Key Configuration

![image](https://github.com/user-attachments/assets/1aa887bc-740b-49b8-a303-82dc545aec7d)

---

### 3. Subtitle Search in Emby

![image](https://github.com/user-attachments/assets/05262f1c-3249-489e-bd7a-23028aee899b)

---

## FAQ

### How do I regenerate my API key?

If you need to regenerate your Jimaku.cc API key, go to the API section of your Jimaku.cc account and click the "Regenerate API Key" button. Make sure to update your Emby plugin with the new key.

### Can I use this plugin for non-anime media?

Yes, the Jimaku.cc service provides subtitles for a variety of content, not just anime. If subtitles exist on Jimaku.cc for your media, they will be fetched accordingly.

---

## Support

If you encounter any issues with the plugin or need further assistance, feel free to open an issue on the GitHub repository. You can also check the Emby forums for community support.

