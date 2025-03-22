# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator is a tool for translating windows of applications on Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## Table of Contents
- [ WindowTranslator](#-windowtranslator)
  - [Table of Contents](#table-of-contents)
  - [Download](#download)
    - [Installer Version ](#installer-version-)
    - [Portable Version](#portable-version)
  - [How to Use](#how-to-use)
    - [Bergamot ](#bergamot-)
  - [Other Features](#other-features)

## Download
### Installer Version ![Recommended](https://img.shields.io/badge/Recommended-brightgreen)

Download `WindowTranslator-(version).msi` from the [GitHub release page](https://github.com/Freeesia/WindowTranslator/releases/latest) and run it to install.  
Installation guide video⬇️  
[![Installation Guide Video](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Portable Version

Download the zip file from the [GitHub release page](https://github.com/Freeesia/WindowTranslator/releases/latest) and extract it to any folder.  
- `WindowTranslator-(version).zip` : Requires .NET environment  
- `WindowTranslator-full-(version).zip` : .NET independent

## How to Use

### Bergamot ![Default](https://img.shields.io/badge/Default-brightgreen)

1. Launch `WindowTranslator.exe` and click the translate button.  
   ![Translate Button](images/translate.png)
2. Select the window of the application you want to translate and click the "OK" button.  
   ![Window Selection](images/select.png)
3. From the "General Settings" tab, select the source and target languages in "Language Settings".  
   ![Language Settings](images/language.png)
4. After completing the settings, click the "OK" button to close the settings screen.  
   > OCR function installation may be required.
   > Please follow the instructions to install.
5. After a while, the translation results will be displayed as an overlay.  
   ![Translation Result](images/result.png)

> [!NOTE]
> Various translation modules are available in WindowTranslator.  
> Google Translate has a low limit on the amount of text that can be translated. If you use it frequently, consider using other modules.  
> You can check the list of available translation modules in the videos below or on the [Wiki](https://github.com/Freeesia/WindowTranslator/wiki#翻訳).
> 
> |                |                                                           How to Use Video                                                            | Advantages                    | Disadvantages                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | Completely free<br/>No translation limit<br/>Fast translation | Lower translation accuracy<br/>Requires more than 1GB of free memory |
> |   Google Translate   | [![Google Translate Setup Video](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Completely free | Low translation limit<br/>Lower translation accuracy |
> |     DeepL      |   [![DeepL Setup Video](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Large free tier<br/>Fast translation | |
> |    GoogleAI    | [![Google AI Setup Video](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | High translation accuracy | Small fee required |
> | LLM (Cloud) | TBD | High translation accuracy | Small fee required |
> | LLM (Local) | TBD | Service itself is free | High-spec PC required |

## Other Features

In addition to translation modules, WindowTranslator has various features.  
If you want to learn more, please check the [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[Privacy Policy](PrivacyPolicy.md)

This document was translated from Japanese using machine translation.