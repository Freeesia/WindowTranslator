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

### ポータブル版

[GitHubのリリースページ](https://github.com/Freeesia/WindowTranslator/releases/latest)からzipファイルをダウンロードして任意のフォルダに展開します

* `WindowTranslator-(バージョン).zip`は.NETがインストールされている環境で動作します
* `WindowTranslator-full-(バージョン).zip`は.NETがインストールされていない環境でも動作します

## 使い方

動画版はYouTubeから⬇️
[![使い方動画](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)

### 事前準備

#### DeepL APIキーの取得

[DeepLのサイト](https://www.deepl.com/ja/pro-api)からユーザー登録を行い、APIキーを取得してください。  
(手元では無料プランのAPIキーにて動作確認を行っていますが、有料プランのAPIキーでも動作すると思います)

> 翻訳エンジンとしてDeepLを利用しています。
> 翻訳に生成AIを利用するには[LLMプラグイン](https://github.com/Freeesia/WindowTranslator/wiki/LLMPlugin)の設定を行ってください。

### 起動

#### 初回設定

1. `WindowTranslator.exe`を起動し、設定画面を開きます。  
  ![設定](images/settings.png)
2. 「全体設定」タブの「言語設定」から翻訳元・翻訳先の言語を選択します。  
  ![言語設定](images/language.png)
3. 「全般設定」タブの「プラグイン設定」から「翻訳モジュール選択」で「DeepL」を選択します。  
  ![プラグイン設定・翻訳モジュール](images/translate_module.png)
4. 「DeepLOptions」タブのAPI Key: DeepLのAPIキーを入力します。
  ![DeepL設定](images/deepl.png)
5. 設定が完了したら「OK」ボタンを押下して設定画面を閉じます。

> 設定ダイアログを閉じる際に、翻訳元の言語を認識するため、OCR機能のインストールが必要になる場合があります。
> 指示に従ってインストールを行うことで、アプリを利用できるようになります。


#### 翻訳の開始

1. `WindowTranslator.exe`を起動し、翻訳ボタンを押下します。  
  ![翻訳ボタン](images/translate.png)
2. 翻訳したいアプリのウィンドウを選択し、「OK」ボタンを押下します。
  ![ウィンドウ選択](images/select.png)
3. 翻訳結果がオーバーレイで表示されます。  
  ![翻訳結果](images/result.png)


### その他の機能

[Wiki](https://github.com/Freeesia/WindowTranslator/wiki)を参照してください。
