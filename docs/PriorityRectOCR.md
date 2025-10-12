# 優先矩形OCR機能 (Priority Rectangle OCR Feature)

## 概要 (Overview)

特定の矩形領域を優先的にOCR処理する機能です。これにより、重要なテキスト領域の認識精度を向上させることができます。
**優先矩形が指定されている場合、全体画面のOCRは実行されず、指定された矩形のみがOCR処理されます。**

This feature allows you to prioritize OCR processing for specific rectangular regions, improving recognition accuracy for important text areas.
**When priority rectangles are specified, full-screen OCR is skipped and only the specified rectangles are processed.**

## 機能詳細 (Feature Details)

### 1. 優先矩形の登録 (Rectangle Registration)

- 複数の矩形を登録可能
- リスト内の順序が優先度を表す（前方が高優先度）
- 各矩形にキーワードを設定可能（翻訳コンテキストとして使用）

Multiple rectangles can be registered, with list order representing priority (higher items have higher priority). Each rectangle can have a keyword that is used as translation context.

### 2. OCR処理 (OCR Processing)

- **優先矩形が設定されている場合**: 指定された矩形のみをOCR処理（全画面OCRはスキップ）
- **優先矩形が設定されていない場合**: 通常の全画面OCR処理
- 矩形は相対座標（0.0-1.0）で保存され、異なる解像度でも動作

**When priority rectangles are configured**: Only the specified rectangles are processed (full-screen OCR is skipped)
**When no priority rectangles are configured**: Normal full-screen OCR processing
Rectangles are stored in relative coordinates (0.0-1.0) to work across different resolutions.

### 3. 設定方法 (Configuration)

#### プログラム的設定 (Programmatic Configuration)

`BasicOcrParam` クラスの `PriorityRects` プロパティに設定します:

```csharp
var ocrParam = new BasicOcrParam
{
    PriorityRects = new List<PriorityRect>
    {
        new PriorityRect(0.1, 0.1, 0.3, 0.2, "メニュー"),
        new PriorityRect(0.5, 0.5, 0.4, 0.3, "ダイアログ")
    }
};
```

#### UI設定 (UI Configuration)

※UI統合は今後の実装予定です。現在は設定ファイルでの直接編集が必要です。

UI integration is planned for future implementation. Currently, direct editing of the configuration file is required.

## 実装詳細 (Implementation Details)

### アーキテクチャ (Architecture)

1. **PriorityRect**: 優先矩形の定義（相対座標、キーワード）
2. **PriorityRectUtility**: OCRモジュール共通のユーティリティクラス
3. **OCR Module Integration**: 各OCRモジュール内で優先矩形を処理

### 処理フロー (Processing Flow)

```
1. RecognizeAsync呼び出し
2. 優先矩形の確認
   ├─ 優先矩形あり → RecognizePriorityRectsAsync
   │   a. 優先矩形ごとに画像を切り出し
   │   b. 切り出した画像をOCR処理
   │   c. 座標を全体画像座標に変換
   │   d. キーワードをコンテキストとして設定
   └─ 優先矩形なし → RecognizeFullScreenAsync
       a. 通常の全画面OCR処理
3. 結果を返す
```

## 翻訳リソース (Translation Resources)

以下の言語でリソースが利用可能です:
- 日本語 (Japanese)
- 英語 (English)
- ドイツ語 (German)
- 韓国語 (Korean)
- 中国語簡体字 (Simplified Chinese)
- 中国語繁体字 (Traditional Chinese)
- ベトナム語 (Vietnamese)

## 今後の予定 (Future Plans)

- [ ] UI統合（設定画面からの矩形登録・編集）
- [ ] 矩形選択UIの完成（ドラッグ選択）
- [ ] リスト順序変更UI（上下移動ボタン）
- [ ] キーワード編集ダイアログ
- [ ] プレビュー機能（登録した矩形の確認）

## 使用例 (Usage Example)

### 設定ファイル (Configuration File)

`%USERPROFILE%\.WindowTranslator\settings.json`:

```json
{
  "Targets": {
    "Default": {
      "PluginParams": {
        "BasicOcrParam": {
          "PriorityRects": [
            {
              "X": 0.1,
              "Y": 0.1,
              "Width": 0.3,
              "Height": 0.2,
              "Keyword": "メニュー"
            },
            {
              "X": 0.5,
              "Y": 0.5,
              "Width": 0.4,
              "Height": 0.3,
              "Keyword": "ダイアログ"
            }
          ]
        }
      }
    }
  }
}
```

## トラブルシューティング (Troubleshooting)

### 矩形が認識されない (Rectangles not recognized)

- 矩形の座標が画像範囲内にあることを確認
- ログを確認（警告メッセージが出力される）

### 重複検出が正しく動作しない (Overlap detection not working correctly)

- TextRect.OverlapsWith()メソッドは回転を考慮した境界ボックスで判定
- デバッグログで重複判定の詳細を確認可能

## 関連ファイル (Related Files)

- `WindowTranslator.Abstractions/PriorityRect.cs`: データモデル
- `WindowTranslator.Abstractions/PriorityRectUtility.cs`: 共通ユーティリティ
- `WindowTranslator.Abstractions/Modules/IOcrModule.cs`: BasicOcrParam拡張
- `WindowTranslator/Modules/Ocr/WindowsMediaOcr.cs`: WindowsMediaOcr実装
- `Plugins/WindowTranslator.Plugin.TesseractOCRPlugin/TesseractOcr.cs`: TesseractOcr実装
- `Plugins/WindowTranslator.Plugin.OneOcrPlugin/OneOcr.cs`: OneOcr実装
- `WindowTranslator/Modules/Ocr/PriorityRectViewModel.cs`: ViewModelクラス
- `WindowTranslator/Modules/Ocr/RectangleSelectionWindow.xaml(.cs)`: 矩形選択UI
- `WindowTranslator.Abstractions/Properties/Resources*.resx`: 翻訳リソース
