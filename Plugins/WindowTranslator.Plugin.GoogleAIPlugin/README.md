# WindowTranslator Google AI Plugin

[WindowTranslator](https://github.com/Freeesia/WindowTranslator) でGoogle AI（Gemini）を利用する多機能プラグインです。

## 機能

- Geminiによる文脈を考慮した翻訳
- 画像を直接送信するAI OCR
- OCRテキストまたは元画像を使った認識結果の補正
- カスタム翻訳コンテキスト、補正サンプル、CSV用語集

AI OCRとOCR補正は実験的な機能です。

## 必要条件と設定

- Google AI APIキー
- 利用するGeminiモデル。必要に応じてプレビュー版モデル名も指定できます。
- OCR補正を使う場合は、補正方法と待機動作を選択します。
- 用語集はヘッダーなしの`原文,訳文`形式のCSVファイルです。

画像やテキストは設定したGoogle AIサービスへ送信されます。利用可能なモデル、料金、上限はサービスの契約内容に従います。

## インストール

WindowTranslatorの設定画面で3番目の「プラグイン」タブを開き、このプラグインをインストールしてください。プレリリース版を利用する場合は、このプラグインの「プレリリース」にチェックを入れます。

インストールまたは更新の反映にはWindowTranslatorの再起動が必要です。
