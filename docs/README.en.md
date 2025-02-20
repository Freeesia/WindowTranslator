# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator is a tool for translating Windows application windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## Download

Download the zip file from the [GitHub Releases page](https://github.com/Freeesia/WindowTranslator/releases/latest) and extract it to any folder.

* `WindowTranslator-(version).zip` works in environments with .NET installed
* `WindowTranslator-full-(version).zip` works even in environments without .NET installed

## Usage

### Prerequisites

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

## What can it do?

WindowTranslator is a tool for translating Windows application windows. It offers the following features:

- Translation of Windows application windows
- Support for multiple languages
- Use of various translation modules

### How to obtain and use the DeepL API key

1. Register as a user on the [DeepL website](https://www.deepl.com/pro-api) and obtain an API key.
2. Launch `WindowTranslator.exe` and open the settings screen.
3. Enter your DeepL API key in the "API Key" field under the "DeepLOptions" tab.
4. Once the settings are complete, click the "OK" button to close the settings screen.

### Other translation modules

WindowTranslator also supports other translation modules. Here are some of their benefits:

- Google Translate: Easy to set up and completely free, but has a low translation limit and may have lower translation accuracy.
- DeepL: Offers a large free tier and fast translations, but may have lower translation accuracy.
- GoogleAI: High translation accuracy, but requires a small fee.
- LLM (Cloud): High translation accuracy, but requires a small fee.
- LLM (Local): The service itself is free, but requires a high-spec PC.
