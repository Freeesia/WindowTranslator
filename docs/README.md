# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslatorは、Windowsのアプリケーションのウィンドウを翻訳するためのツールです。

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## 目次
- [ WindowTranslator](#-windowtranslator)
  - [目次](#目次)
  - [ダウンロード](#ダウンロード)
    - [インストール版 ](#インストール版-)
    - [ポータブル版](#ポータブル版)
  - [使い方](#使い方)
    - [Google翻訳 ](#google翻訳-)
  - [その他の機能](#その他の機能)

## ダウンロード
### インストール版 ![オススメ](https://img.shields.io/badge/%E3%82%AA%E3%82%B9%E3%82%B9%E3%83%A1-brightgreen)

[GitHubのリリースページ](https://github.com/Freeesia/WindowTranslator/releases/latest)から`WindowTranslator-(バージョン).msi`をダウンロードして実行しインストールします。  
インストール手順動画はこちら:  
[![インストール手順動画](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### ポータブル版

[GitHubのリリースページ](https://github.com/Freeesia/WindowTranslator/releases/latest)からzipファイルをダウンロードして任意のフォルダに展開してください。  
- `WindowTranslator-(バージョン).zip` : .NET環境が必要  
- `WindowTranslator-full-(バージョン).zip` : .NET非依存

## 使い方

> [!NOTE]
> WindowTranslatorでは各種翻訳モジュールが利用可能です。ここではデフォルトのGoogle翻訳を用いた方法を示します。  
> Google翻訳は翻訳可能なテキスト量が少なく、利用頻度が高い場合は他のモジュールの利用を検討してください。  
> 利用可能な翻訳モジュール一覧は下記の動画もしくは[Wiki](https://github.com/Freeesia/WindowTranslator/wiki#翻訳)でご確認いただけます。
> 
> |                |                                                              使い方動画                                                               | メリット                    | デメリット                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :-------------------------- | :-------------------------------- |
> |   Google翻訳   |                                                                  TBD                                                                  | 導入が容易<br/>完全無料     | 翻訳上限が低い<br/>翻訳精度が劣る |
> |     DeepL      |   [![DeepL設定動画](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | 無料枠が多い<br/>翻訳が速い | 翻訳精度が劣る                    |
> |    GoogleAI    | [![Google AI設定動画](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | 翻訳精度が高い              | 少額の課金が必要                  |
> | LLM (クラウド) |                                                                  TBD                                                                  | 翻訳精度が高い              | 少額の課金が必要                  |
> | LLM (ローカル) |                                                                  TBD                                                                  | サービス自体は無料          | 高スペックなPCが必要              |

### Google翻訳 ![デフォルト](https://img.shields.io/badge/デフォルト-brightgreen)

1. `WindowTranslator.exe`を起動し、翻訳ボタンを押下します。  
   ![翻訳ボタン](images/translate.png)
2. 翻訳したいアプリのウィンドウを選択し、「OK」ボタンを押下します。  
   ![ウィンドウ選択](images/select.png)
3. 「全体設定」タブの「言語設定」から翻訳元・翻訳先を選択します。  
   ![言語設定](images/language.png)
4. 設定完了後、「OK」ボタンを押下して設定画面を閉じます。  
   > OCR機能のインストールが必要な場合があります。
   > 指示に従いインストールしてください。
5. ブラウザが起動し、Googleのログイン画面が表示されます。  
   ![ログイン画面](images/login.png)
6. ログイン後、アクセス許可を求められるので「すべて選択」し「続行」ボタンを押下します。
   ![認可画面](images/auth.png)
7. しばらくすると翻訳結果がオーバーレイで表示されます。  
   ![翻訳結果](images/result.png)

## その他の機能

[Wiki](https://github.com/Freeesia/WindowTranslator/wiki) をご確認ください。

---
[プライバシーポリシー](PrivacyPolicy.md)