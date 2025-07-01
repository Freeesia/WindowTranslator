# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)

WindowTranslator是一款用于翻译Windows应用程序窗口的工具。

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md)

## 目录
- [ WindowTranslator](#-windowtranslator)
  - [目录](#目录)
  - [下载](#下载)
    - [安装版 ](#安装版-)
    - [便携版](#便携版)
  - [使用方法](#使用方法)
    - [Bergamot ](#bergamot-)
  - [其他功能](#其他功能)

## 下载
### 安装版 ![推荐](https://img.shields.io/badge/推荐-brightgreen)

从[GitHub发布页面](https://github.com/Freeesia/WindowTranslator/releases/latest)下载`WindowTranslator-(版本).msi`并运行安装。  
安装指南视频⬇️  
[![安装指南视频](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### 便携版

从[GitHub发布页面](https://github.com/Freeesia/WindowTranslator/releases/latest)下载zip文件并解压到任意文件夹。  
- `WindowTranslator-(版本).zip` : 需要.NET环境  
- `WindowTranslator-full-(版本).zip` : 不依赖.NET

## 使用方法

### Bergamot ![默认](https://img.shields.io/badge/默认-brightgreen)

1. 启动`WindowTranslator.exe`并点击翻译按钮。  
   ![翻译按钮](images/translate.png)
2. 选择要翻译的应用程序窗口，然后点击"确定"按钮。  
   ![窗口选择](images/select.png)
3. 在"常规设置"选项卡中的"语言设置"中选择源语言和目标语言。  
   ![语言设置](images/language.png)
4. 完成设置后，点击"确定"按钮关闭设置界面。  
   > 可能需要安装OCR功能。
   > 请按照指示进行安装。
5. 片刻后，翻译结果将以叠加形式显示。  
   ![翻译结果](images/result.png)

> [!NOTE]
> WindowTranslator提供各种翻译模块。  
> Google翻译可翻译的文本量较少，如果您经常使用，请考虑使用其他模块。  
> 您可以在下面的视频或[Wiki](https://github.com/Freeesia/WindowTranslator/wiki#翻訳)中查看可用翻译模块列表。
> 
> |                |                                                          使用方法视频                                                           | 优点                    | 缺点                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | 完全免费<br/>无翻译限制<br/>翻译速度快 | 翻译准确度较低<br/>需要1GB以上空闲内存 |
> |   Google翻译   | [![Google翻译设置视频](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | 完全免费 | 翻译限制低<br/>翻译准确度较低 |
> |     DeepL      |   [![DeepL设置视频](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | 免费额度大<br/>翻译速度快 | |
> |     Gemini     | [![Google AI设置视频](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | 翻译准确度高 | 需要少量付费 |
> | ChatGPT (云端) | TBD | 翻译准确度高 | 需要少量付费 |
> | ChatGPT (本地) | TBD | 服务本身免费 | 需要高配置PC |

## 其他功能

除了翻译模块外，WindowTranslator还具有各种功能。  
如果您想了解更多，请查看[Wiki](https://github.com/Freeesia/WindowTranslator/wiki)。

---
[隐私政策](PrivacyPolicy.md)

本文档是使用机器翻译从日语翻译而来。