# WindowTranslator OneOCR Plugin

[WindowTranslator](https://github.com/Freeesia/WindowTranslator) で、WindowsのSnipping Toolに含まれるOneOCRエンジンを利用するOCRプラグインです。

## 機能

- WindowsのローカルOCRエンジンによる高速な文字認識
- OCR領域の結合、傾き、拡大率、明るさ、コントラストを考慮した後処理
- 認識結果から翻訳先言語のテキストを除外

## 必要条件

- OneOCRを含む対応バージョンのWindows Snipping Tool
- 初回設定時に、Snipping Toolから必要なOneOCRコンポーネントをWindowTranslatorの共有データ領域へコピーできること

対応するSnipping Toolが見つからない場合は、WindowTranslatorからMicrosoft Storeを開いて更新できます。OneOCR本体とモデルは、このNuGetパッケージには含まれません。

## インストール

WindowTranslatorの設定画面で3番目の「プラグイン」タブを開き、このプラグインをインストールしてください。プレリリース版を利用する場合は、このプラグインの「プレリリース」にチェックを入れます。

インストールまたは更新の反映にはWindowTranslatorの再起動が必要です。

## 参考

- [SnippingToolOcrSharp](https://github.com/ksasao/SnippingToolOcrSharp)
