# WindowTranslator DeepL Translator Plugin

[WindowTranslator](https://github.com/Freeesia/WindowTranslator) でDeepL APIを利用する翻訳プラグインです。

## 機能

- DeepL APIによる翻訳
- WindowTranslatorから渡された文脈を翻訳リクエストへ反映
- CSV用語集による表記の統一
- 設定画面からAPI利用量を確認

## 設定

- DeepL APIの認証キーが必要です。
- 用語集を利用する場合は、ヘッダーなしの`原文,訳文`形式のCSVファイルを指定します。
- 利用可能な言語、料金、上限はDeepL APIの契約内容に従います。

## インストール

WindowTranslatorの設定画面で3番目の「プラグイン」タブを開き、このプラグインをインストールしてください。プレリリース版を利用する場合は、このプラグインの「プレリリース」にチェックを入れます。

インストールまたは更新の反映にはWindowTranslatorの再起動が必要です。
