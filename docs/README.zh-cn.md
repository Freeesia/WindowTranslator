# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator是一款用于翻译Windows应用程序窗口的工具。

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## 下载

### 安装版 ![推荐]
从 [GitHub 发布页面](https://github.com/Freeesia/WindowTranslator/releases/latest)下载 `WindowTranslator-(版本).msi` 并运行进行安装。  
安装步骤演示视频：  
[![安装视频](https://github.com/user-attachments/assets/视频链接)](https://youtu.be/视频ID)

### 便携版
从 [GitHub 发布页面](https://github.com/Freeesia/WindowTranslator/releases/latest)下载 zip 文件，并解压到任意文件夹。  
* `WindowTranslator-(版本).zip`：适用于已安装 .NET 环境的系统。  
* `WindowTranslator-full-(版本).zip`：适用于未安装 .NET 环境的系统。

## 使用方法

### 视频版
|            | DeepL 版本             | Google AI 版本            |
| :--------: | ---------------------- | ------------------------- |
| 视频链接   | [![DeepL 设置视频](https://github.com/user-attachments/assets/视频链接1)](https://youtu.be/视频ID1) | [![Google AI 设置视频](https://github.com/user-attachments/assets/视频链接2)](https://youtu.be/视频ID2) |
| 优点       | 翻译迅速，免费额度充足   | 翻译准确性较高             |
| 缺点       | 翻译准确性略低         | 需少量付费，翻译稍慢       |

### 事前准备

#### 获取 DeepL API 密钥
请前往 [DeepL 网站](https://www.deepl.com/ja/pro-api) 注册并获取您的 API 密钥。

> 本工具使用 DeepL 作为翻译引擎。  
> 若需使用生成型 AI 翻译，请参考 [LLM Plugin](https://github.com/Freeesia/WindowTranslator/wiki/LLMPlugin) 进行配置。

### 启动

#### 初次设置

1. 运行 `WindowTranslator.exe` 打开设置界面。  
   ![设置](images/settings.png)
2. 在“整体设置”标签下的“语言设置”中，选择翻译原语言及目标语言。  
   ![语言设置](images/language.png)
3. 在“插件设置”中选择翻译模块为 “DeepL”。  
   ![插件设置](images/translate_module.png)
4. 在 “DeepLOptions” 标签中输入您的 DeepL API 密钥。  
   ![DeepL 设置](images/deepl.png)
5. 设置完成后，点击“OK”关闭设置界面。

#### 开始翻译

1. 运行 `WindowTranslator.exe`，点击“翻译”按钮。  
   ![翻译按钮](images/translate.png)
2. 选择需要翻译的应用窗口，并点击“确定”。  
   ![窗口选择](images/select.png)
3. 翻译结果将以覆盖层形式显示。  
   ![翻译结果](images/result.png)

[Wiki](https://github.com/Freeesia/WindowTranslator/wiki)

---
隐私政策: [隐私政策](PrivacyPolicy.md)

> ※ 本文档为机器翻译。