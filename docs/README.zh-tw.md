# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator是一個用於翻譯Windows應用程序窗口的工具。

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## 下載

從 [GitHub的發布頁面](https://github.com/Freeesia/WindowTranslator/releases/latest)下載zip文件，然後解壓到任意文件夾。

* `WindowTranslator-(版本).zip`可以在已安裝.NET的環境中運行。
* `WindowTranslator-full-(版本).zip`可在未安裝.NET的環境中運行。

## 使用方法

### 預備工作

#### 獲取DeepL API密鑰

請訪問 [DeepL的網站](https://www.deepl.com/zh/pro-api) 註冊用戶，並獲取API密鑰。  
(我們使用免費計劃的API密鑰進行了測試，但認為付費計劃的API密鑰也可以使用)

### 啟動

#### 首次設置

1. 啟動`WindowTranslator.exe`，打開設置界面。   
  ![設置](images/settings.png)
2. 在“全局設置”標籤頁的“語言設置”中選擇翻譯來源和翻譯目標語言。   
  ![語言設置](images/language.png)
3. 在“DeepLOptions”標籤頁輸入DeepL的API密鑰。   
  ![DeepL設置](images/deepl.png)
4. 完成設置後，點擊“確定”按鈕關閉設置界面。

#### 開始翻譯

1. 啟動`WindowTranslator.exe`，點擊翻譯按鈕。   
  ![翻譯按鈕](images/translate.png)
2. 選擇要翻譯的應用程序窗口，然後點擊“確定”按鈕。   
  ![窗口選擇](images/select.png)
3. 翻譯結果將以覆蓋層的形式顯示。   
  ![翻譯結果](images/result.png)

> Translated with ChatGPT