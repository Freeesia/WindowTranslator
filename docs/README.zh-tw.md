# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator是一個用於翻譯Windows應用程序窗口的工具。

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## 下載

### 安裝版 ![推薦](https://img.shields.io/badge/推薦-brightgreen)
從 [GitHub 的發布頁面](https://github.com/Freeesia/WindowTranslator/releases/latest)下載 `WindowTranslator-(版本).msi` 並執行安裝。  

安裝步驟示範影片：  
[![安裝影片](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### 便攜版
從 [GitHub 的發布頁面](https://github.com/Freeesia/WindowTranslator/releases/latest)下載 zip 文件並解壓至任意資料夾。  
* `WindowTranslator-(版本).zip`：適用於已安裝 .NET 環境的系統。  
* `WindowTranslator-full-(版本).zip`：適用於未安裝 .NET 環境的系統。

## 使い方

### 影片版
|            | DeepL 版本             | Google AI 版本            |
| :--------: | ---------------------- | ------------------------- |
| 影片連結   | [![DeepL 設定影片](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0) | [![Google AI 設定影片](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) |
| 優點       | 翻譯快速、免費額度充足   | 翻譯精度較高               |
| 缺點       | 翻譯精度稍低           | 需少額付費，翻譯較慢       |

### 事前準備

#### 取得 DeepL API 金鑰
請前往 [DeepL 網站](https://www.deepl.com/ja/pro-api) 註冊並取得您的 API 金鑰。

> 本工具以 DeepL 作為翻譯引擎，若需使用生成型 AI 翻譯，請參照 [LLM Plugin](https://github.com/Freeesia/WindowTranslator/wiki/LLMPlugin) 進行設定。

### 起動

#### 初次設定

1. 執行 `WindowTranslator.exe` 開啟設定畫面。  
   ![設定](images/settings.png)
2. 在「全體設定」標籤的「語言設定」中選擇翻譯原語及目標語。  
   ![語言設定](images/language.png)
3. 於「插件設定」中，選擇翻譯模組為「DeepL」。  
   ![插件設定](images/translate_module.png)
4. 在「DeepLOptions」標籤中輸入您的 DeepL API 金鑰。  
   ![DeepL 設定](images/deepl.png)
5. 設定完成後，點擊「確定」關閉設定畫面。

#### 開始翻譯

1. 執行 `WindowTranslator.exe`，點擊「翻譯」按鈕。  
   ![翻譯按鈕](images/translate.png)
2. 選擇欲翻譯的應用程式窗口，然後點選「確定」。  
   ![窗口選擇](images/select.png)
3. 翻譯結果將以覆蓋層形式顯示。  
   ![翻譯結果](images/result.png)

---  
隱私政策: [隱私政策](PrivacyPolicy.zh-tw.md)

> ※ 本文件為機器翻譯。