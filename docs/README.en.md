# WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub version](https://badge.fury.io/gh/Freeesia%2FWindowTranslator.svg)](https://badge.fury.io/gh/Freeesia%2FWindowTranslator)
[![NuGet version](https://badge.fury.io/nu/WindowTranslator.Abstractions.svg)](https://badge.fury.io/nu/WindowTranslator.Abstractions)

WindowTranslator is a tool for translating Windows application windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## Download

Download the zip file from the [GitHub Releases page](https://github.com/Freeesia/WindowTranslator/releases/latest) and extract it to any folder.

* `WindowTranslator-(version).zip` works in environments with .NET installed
* `WindowTranslator-full-(version).zip` works even in environments without .NET installed

## Usage

### Prerequisites

#### Language Settings

Add the source and target languages for translation to your Windows language settings.  
[How to add languages to Windows](https://support.microsoft.com/en-us/windows/language-packs-for-windows-a5094319-a92d-18de-5b53-1cfc697cfca8)

#### Obtain DeepL API Key

Register as a user on the [DeepL website](https://www.deepl.com/pro-api) and obtain an API key.  
(The API key for the free plan has been tested, but it is expected to work with paid plan API keys as well)

### Launch

#### Initial Setup

1. Launch `WindowTranslator.exe` and open the settings screen.  
  ![Settings](images/settings.png)
2. Select the source and target languages in the "Language Settings" under the "General Settings" tab.  
  ![Language Settings](images/language.png)
3. Enter your DeepL API key in the "API Key" field under the "DeepLOptions" tab.  
  ![DeepL Settings](images/deepl.png)
4. Once the settings are complete, click the "OK" button to close the settings screen.

#### Starting Translation

1. Launch `WindowTranslator.exe` and click the "Translate" button.  
  ![Translate Button](images/translate.png)
2. Select the window of the application you want to translate and click the "OK" button.  
  ![Window Selection](images/select.png)
3. The translation result will be displayed as an overlay.  
  ![Translation Result](images/result.png)

### Other Settings

#### Display Translation Results in a Separate Window

You can display the translation results in a separate window.  
In the settings screen, select "Capture Window" in "Translation Result Display Mode" under the "General Settings" tab, then click the "OK" button to close the settings screen.   
![Display Mode Settings](images/settings_window.png)

When you select the application you want to translate, the translation result will be displayed in a separate window.   
![Window Mode](images/window_mode.png)

#### Always Translate a Specific Application Window

You can set WindowTranslator to automatically detect and start translating a specific application when it is launched.

1. Launch `WindowTranslator.exe` and open the settings screen.  
  ![Settings](images/settings.png)
2. Click the "Execute" button for the "Register to startup command" under the "SettingsViewModel" tab to set it to start automatically when you log on.   
  ![Startup Registration](images/startup.png)
3. Enter the process name of the application you want to translate in "Auto Translate Target" under the "General Settings" tab  
  ![Auto Translate Target](images/always_translate.png)
  * By checking the "Automatically translate selected process when it is launched" option, the process will be automatically registered as a translation target.
4. Once the settings are complete, click the "OK" button to close the settings screen.
5. From now on, when the target process is launched, a notification will appear asking if you want to start translation.  
  ![Notification](images/notify.png)

##### If the Notification is Not Displayed

If the notification is not displayed, it is possible that "Focus Assist" is enabled.  
Please follow the steps below to enable notifications.

1. Open the "Notifications" settings under "System" in the Windows "Settings".  
 ![Settings](images/win_settings.png)
2. Select "Automatically turn on Focus Assist" and uncheck "When I'm using an app in full screen mode".   
  ![Focus Assist](images/full.png)
3. Click "Add an app" under "Set priority notifications".  
 ![Notification Settings](images/notification.png)
 ![Priority Notifications](images/priority.png)
4. Select "WindowTranslator".   
  ![Select App](images/select_app.png)

> Translated with ChatGPT