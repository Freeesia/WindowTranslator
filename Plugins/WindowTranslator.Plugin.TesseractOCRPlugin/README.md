# WindowTranslator Tesseract OCR Plugin

[WindowTranslator](https://github.com/Freeesia/WindowTranslator) で、オープンソースのTesseract OCRエンジンを利用するプラグインです。

## 機能

- Tesseractによる多言語OCR
- 翻訳元言語に対応する`traineddata`を初回利用時に自動取得
- OCR領域の結合、拡大率、明るさ、コントラストを考慮した後処理

## 必要条件

- Microsoft Visual C++ 2015以降のx64ランタイム
- 言語データを初めて取得するときのインターネット接続

必要なVisual C++ランタイムがない場合は、WindowTranslatorからインストールできます。言語データは`tesseract-ocr/tessdata_best`から取得されます。

## インストール

WindowTranslatorの設定画面で3番目の「プラグイン」タブを開き、このプラグインをインストールしてください。プレリリース版を利用する場合は、このプラグインの「プレリリース」にチェックを入れます。

インストールまたは更新の反映にはWindowTranslatorの再起動が必要です。
