# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator is a tool for translating the windows of Windows applications.

[JA](README.md) | [EN](README.en.md) | [DE](README.de.md) | [KR](README.kr.md) | [ZH-CN](README.zh-cn.md) | [ZH-TW](README.zh-tw.md)

## Table of Contents
- [ WindowTranslator](#-windowtranslator)
  - [Table of Contents](#table-of-contents)
  - [Download](#download)
    - [Installer Version ](#installer-version-)
    - [Portable Version](#portable-version)
  - [Usage](#usage)
    - [Google Translate ](#google-translate-)
  - [Other Features](#other-features)

## Download
### Installer Version ![Recommended](https://img.shields.io/badge/%E3%82%AA%E3%82%B9%E3%82%B9%E3%83%A1-brightgreen)
Download the `WindowTranslator-(version).msi` from the [GitHub Releases page](https://github.com/Freeesia/WindowTranslator/releases/latest) and run it to install.  
Installation video available here:  
[![Installation Video](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Portable Version
Download the zip file from the [GitHub Releases page](https://github.com/Freeesia/WindowTranslator/releases/latest) and extract it to a folder.  
- `WindowTranslator-(version).zip` requires a .NET environment.  
- `WindowTranslator-full-(version).zip` is independent of .NET.

## Usage

> [!NOTE]
> WindowTranslator supports various translation modules. The default method shown here uses Google Translate.  
> Google Translate has a lower character limit so for heavy usage you might consider other modules.  
> For a full list of available translation modules, please refer to the video below or [Wiki](https://github.com/Freeesia/WindowTranslator/wiki#translation).
> 
> |         |                                Usage Video                                 | Advantages                    | Disadvantages                        |
> | ------- | :------------------------------------------------------------------------: | ----------------------------- | ------------------------------------ |
> | Google  |                                  TBD                                       | Easy to set up<br/>Free       | Low translation limit<br/>Lower accuracy |
> | DeepL   | [![DeepL Setup Video](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0) | Generous free tier<br/>Fast translation | Lower accuracy                    |
> | GoogleAI| [![Google AI Setup Video](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | High accuracy                | Requires a small fee                 |
> | LLM (Cloud)|                                TBD                                     | High accuracy                | Requires a small fee                 |
> | LLM (Local)|                                TBD                                     | Free service                 | High-end PC required                 |

### Google Translate ![Default](https://img.shields.io/badge/Default-brightgreen)

1. Launch `WindowTranslator.exe` and click the Translate button.  
   ![Translate Button](images/translate.png)
2. Select the window of the application you want to translate and click "OK".  
   ![Window Selection](images/select.png)
3. In the "General Settings" tab under "Language Settings", choose the source and target languages.  
   ![Language Settings](images/language.png)
4. After settings are applied, click "OK" to close the settings dialog.  
   > If OCR is required, follow the on-screen instructions to install it.
5. The browser will open to display the Google login screen.  
   ![Login Screen](images/login.png)
6. After login, select "Select All" for permissions and click "Continue".  
   ![Authorization Screen](images/auth.png)
7. Shortly thereafter, the translation result will appear as an overlay.  
   ![Translation Result](images/result.png)

## Other Features

Please refer to the [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---  
Privacy Policy: [Privacy Policy](PrivacyPolicy.en.md)

> ※ This document was machine translated.