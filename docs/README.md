# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslatorは、Windowsのアプリケーションのウィンドウを翻訳するためのツールです。

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## ダウンロード
### インストール版 ![オススメ](https://img.shields.io/badge/%E3%82%AA%E3%82%B9%E3%82%B9%E3%83%A1-brightgreen)

[GitHubのリリースページ](https://github.com/Freeesia/WindowTranslator/releases/latest)から`WindowTranslator-(バージョン).msi`をダウンロードおよび実行してインストールを行います。

インストール手順の動画はこちらから⬇️
[![インストール手順動画](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### ポータブル版

[GitHubのリリースページ](https://github.com/Freeesia/WindowTranslator/releases/latest)からzipファイルをダウンロードして任意のフォルダに展開します

* `WindowTranslator-(バージョン).zip`は.NETがインストールされている環境で動作します
* `WindowTranslator-full-(バージョン).zip`は.NETがインストールされていない環境でも動作します

## 使い方

> [!NOTE]
> WindowTranslatorでは様々な翻訳モジュールを利用可能ですが、ここではデフォルトのGoogle翻訳を利用する手順を紹介します。  
> Google翻訳は翻訳可能なテキスト量が少ないので、利用頻度が高くなる場合は他の翻訳モジュールをご利用ください。  
> 利用可能な翻訳モジュール一覧は以下をご覧ください。

動画版はYouTubeから⬇️

|                |                                                              使い方動画                                                               | メリット                    | デメリット                         |
| :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :-------------------------- | :--------------------------------- |
|   Google翻訳   |                                                                  TBD                                                                  | 導入が容易<br/>完全無料     | 翻訳上限が低い<br/> 翻訳精度が劣る |
|     DeepL      |   [![DeepL設定動画](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | 無料枠が多い<br/>翻訳が速い | 翻訳精度が劣る                     |
|    GoogleAI    | [![Google AI設定動画](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | 翻訳精度が高い              | 少額の課金が必要                   |
| LLM (クラウド) |                                                                  TBD                                                                  | 翻訳精度が高い              | 少額の課金が必要                   |
| LLM (ローカル) |                                                                  TBD                                                                  | サービス自体は無料          | 高スペックなPCが必要               |

### Google翻訳 (デフォルト)

1. `WindowTranslator.exe`を起動し、翻訳ボタンを押下します。  
  ![翻訳ボタン](images/translate.png)
1. 翻訳したいアプリのウィンドウを選択し、「OK」ボタンを押下します。
  ![ウィンドウ選択](images/select.png)
1. 「全体設定」タブの「言語設定」から翻訳元・翻訳先の言語を選択します。  
  ![言語設定](images/language.png)
1. 設定が完了したら「OK」ボタンを押下して設定画面を閉じます。
    > 設定ダイアログを閉じる際に、翻訳元の言語を認識するため、OCR機能のインストールが必要になる場合があります。
    > 指示に従ってインストールを行うことで、アプリを利用できるようになります。
3. 翻訳結果がオーバーレイで表示されます。  
  ![翻訳結果](images/result.png)


### その他の機能

[Wiki](https://github.com/Freeesia/WindowTranslator/wiki)を参照してください。
