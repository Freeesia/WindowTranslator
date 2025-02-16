# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator is a tool for translating Windows application windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## Download

### Installation Version ![Recommended](https://img.shields.io/badge/Recommended-brightgreen)
Download the MSI file from the [GitHub Releases page](https://github.com/Freeesia/WindowTranslator/releases/latest) and run it to install WindowTranslator.

### Portable Version
Download the zip file from the [GitHub Releases page](https://github.com/Freeesia/WindowTranslator/releases/latest) and extract it to any folder.

## Usage

### Video Version
|               | DeepL Version | Google AI Version |
| ------------- | ------------- | ----------------- |
| Video Link    | [![DeepL Settings Video](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0) | [![Google AI Settings Video](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) |
| Advantages    | Fast translation, generous free quota | Higher translation accuracy |
| Disadvantages | Lower translation accuracy | Minor payment required, slower translation |

### Prerequisites

#### Obtain DeepL API Key
Register on the [DeepL website](https://www.deepl.com/pro-api) to get your API key.  
(Works with both free and paid plans)

> DeepL is used as the translation engine.  
> For using generative AI translation, configure the [LLM Plugin](https://github.com/Freeesia/WindowTranslator/wiki/LLMPlugin).

### Launch

#### Initial Setup

1. Launch `WindowTranslator.exe` and open the settings window.  
   ![Settings](images/settings.png)
2. In the "General Settings" tab, select the source and target languages under "Language Settings".  
   ![Language Settings](images/language.png)
3. In the "Plugin Settings" tab, select "DeepL" under "Translation Module".  
   ![Translation Module](images/translate_module.png)
4. Enter your DeepL API key in the "DeepLOptions" tab.  
   ![DeepL Settings](images/deepl.png)
5. Click "OK" to close the settings window.

> When closing the settings dialog, OCR functionality may need to be installed to recognize the source language. Follow the prompts if necessary.

#### Starting Translation

1. Launch `WindowTranslator.exe` and click the "Translate" button.  
   ![Translate Button](images/translate.png)
2. Select the window of the application to be translated and click "OK".  
   ![Window Selection](images/select.png)
3. The translation result is displayed as an overlay.  
   ![Translation Result](images/result.png)

## Other Features

For more details, please refer to the [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---  
Privacy Policy: [Privacy Policy](PrivacyPolicy.en.md)

> ※ This document was machine translated.