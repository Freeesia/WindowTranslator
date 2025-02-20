# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator是一个用于翻译Windows应用程序窗口的工具。

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## 下载

从 [GitHub的发布页面](https://github.com/Freeesia/WindowTranslator/releases/latest)下载zip文件，然后解压到任意文件夹。

* `WindowTranslator-(版本).zip`可以在已安装.NET的环境中运行。
* `WindowTranslator-full-(版本).zip`可在未安装.NET的环境中运行。

## 使用方法

### 准备工作

#### 获取DeepL API密钥

请访问 [DeepL的网站](https://www.deepl.com/zh/pro-api) 注册用户，并获取API密钥。  
(我们使用免费计划的API密钥进行了测试，但认为付费计划的API密钥也可以使用)

### 启动

#### 首次设置

1. 启动`WindowTranslator.exe`，打开设置界面。   
  ![设置](images/settings.png)
2. 在“全局设置”标签页的“语言设置”中选择翻译来源和翻译目标语言。   
  ![语言设置](images/language.png)
3. 在“DeepLOptions”标签页输入DeepL的API密钥。   
  ![DeepL设置](images/deepl.png)
4. 完成设置后，点击“确定”按钮关闭设置界面。

#### 开始翻译

1. 启动`WindowTranslator.exe`，点击翻译按钮。   
  ![翻译按钮](images/translate.png)
2. 选择要翻译的应用程序窗口，然后点击“确定”按钮。   
  ![窗口选择](images/select.png)
3. 翻译结果将以覆盖层的形式显示。   
  ![翻译结果](images/result.png)

## 它能做什么？

WindowTranslator是一个用于翻译Windows应用程序窗口的工具。它提供以下功能：

- 翻译Windows应用程序窗口
- 支持多种语言
- 使用各种翻译模块

### 如何获取和使用DeepL API密钥

1. 请访问 [DeepL的网站](https://www.deepl.com/pro-api) 注册用户，并获取API密钥。
2. 启动`WindowTranslator.exe`，打开设置界面。
3. 在“DeepLOptions”标签页的“API Key”字段中输入您的DeepL API密钥。
4. 完成设置后，点击“确定”按钮关闭设置界面。

### 其他翻译模块

WindowTranslator还支持其他翻译模块。以下是它们的一些优点：

- Google翻译：设置简单且完全免费，但翻译限制较低，翻译准确性可能较低。
- DeepL：提供大量免费配额和快速翻译，但翻译准确性可能较低。
- GoogleAI：翻译准确性高，但需要支付少量费用。
- LLM（云端）：翻译准确性高，但需要支付少量费用。
- LLM（本地）：服务本身免费，但需要高规格的PC。
