# WindowTranslator PLaMo Translator Plugin

[WindowTranslator](https://github.com/Freeesia/WindowTranslator) で、日本語に強いPLaMo 2 Translateモデルをローカル実行する翻訳プラグインです。

## 機能

- LLamaSharpを使用したローカル翻訳
- 翻訳テキストを外部サービスへ送信せずに処理
- 初回利用時に量子化済みPLaMo翻訳モデルを自動取得
- コンテキスト長と使用するVRAM量を設定可能

## 必要条件

- 64ビット版Windows
- 十分な空きストレージとメモリ
- CUDAに対応するNVIDIA GPUとドライバーを推奨
- モデルを初めて取得するときのインターネット接続

モデル取得後の翻訳はローカルで実行されます。用語集と追加コンテキストには対応していません。

## インストール

WindowTranslatorの設定画面で3番目の「プラグイン」タブを開き、このプラグインをインストールしてください。プレリリース版を利用する場合は、このプラグインの「プレリリース」にチェックを入れます。

インストールまたは更新の反映にはWindowTranslatorの再起動が必要です。
