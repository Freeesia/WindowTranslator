# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator 是一個用於翻譯 Windows 應用程式視窗內容的工具。

[JA](README.md) | [EN](README.en.md) | [DE](README.de.md) | [KR](README.kr.md) | [ZH-CN](README.zh-cn.md) | [ZH-TW](README.zh-tw.md)

## 目錄
- [ WindowTranslator](#-windowtranslator)
  - [目錄](#目錄)
  - [下載](#下載)
    - [安裝版 ](#安裝版-)
    - [便攜版](#便攜版)
  - [使用方法](#使用方法)
    - [Google 翻譯 ](#google-翻譯-)
  - [其他功能](#其他功能)

## 下載
### 安裝版 ![推薦](https://img.shields.io/badge/%E3%82%AA%E3%82%B9%E3%82%B9%E3%83%A1-brightgreen)
請從 [GitHub Releases 頁面](https://github.com/Freeesia/WindowTranslator/releases/latest) 下載 `WindowTranslator-(版本).msi` 並執行進行安裝。  
安裝影片如下：  
[![安裝影片](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### 便攜版
請從 [GitHub Releases 頁面](https://github.com/Freeesia/WindowTranslator/releases/latest) 下載壓縮檔並解壓至任意資料夾。  
- `WindowTranslator-(版本).zip`：需要 .NET 環境。  
- `WindowTranslator-full-(版本).zip`：不依賴 .NET。

## 使用方法

### Google 翻譯 ![預設](https://img.shields.io/badge/Default-brightgreen)

1. 啟動 `WindowTranslator.exe` 並點選翻譯按鈕。  
   ![翻譯按鈕](images/translate.png)
2. 選擇要翻譯的應用程式視窗，然後點選「OK」。  
   ![視窗選擇](images/select.png)
3. 在「通用設定」頁籤中的「語言設定」選擇原始語言和目標語言。  
   ![語言設定](images/language.png)
4. 設定完成後，點選「OK」關閉設定對話框。  
   > 若需要 OCR 功能，請依照指示安裝。
5. 瀏覽器將啟動並顯示 Google 登入畫面。  
   ![登入畫面](images/login.png)
6. 登入後，選擇「全部選取」以授權，然後點擊「繼續」。  
   ![授權畫面](images/auth.png)
7. 稍後翻譯結果將以浮層方式顯示。  
   ![翻譯結果](images/result.png)

> [!NOTE]
> WindowTranslator 支援多種翻譯模組，此處展示預設的 Google 翻譯使用法。  
> 由於 Google 翻譯限定翻譯文字量，若使用頻率較高請考慮其他模組。  
> 更多可用翻譯模組請參考下方影片或 [Wiki](https://github.com/Freeesia/WindowTranslator/wiki#translation)。
> 
> |              |                                  使用影片                                  | 優點                         | 缺點                                |
> | ------------ | :-----------------------------------------------------------------------: | ---------------------------- | ----------------------------------- |
> | Google 翻譯  |                                  TBD                                      | 設定簡單<br/>完全免費         | 翻譯上限低<br/>準確度較低              |
> | DeepL        | [![DeepL 設定影片](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0) | 免費額度高<br/>翻譯迅速         | 準確度較低                         |
> | GoogleAI     | [![Google AI 設定影片](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | 準確度高                    | 需要少量付費                         |
> | LLM (雲端)    |                                  TBD                                      | 準確度高                    | 需要少量付費                         |
> | LLM (本機)    |                                  TBD                                      | 免費服務                    | 需要高規格電腦                       |

## 其他功能

請參閱 [Wiki](https://github.com/Freeesia/WindowTranslator/wiki) 了解更多功能。

---  
隱私政策: [隱私政策](PrivacyPolicy.zh-tw.md)

> ※ 本文件為機器翻譯。