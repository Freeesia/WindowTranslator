# 設定例 (Configuration Examples)

このディレクトリには、WindowTranslatorの設定ファイルの例が含まれています。

This directory contains example configuration files for WindowTranslator.

## settings-with-priority-rects.json

優先矩形OCR機能を使用した設定例です。

Example configuration using the Priority Rectangle OCR feature.

### 使い方 (Usage)

1. WindowTranslatorを一度起動して終了します（設定フォルダが作成されます）
2. `%USERPROFILE%\.WindowTranslator\settings.json` を開きます
3. この例のファイル内容をコピーして貼り付けます
4. 必要に応じて矩形の座標やキーワードを調整します
5. WindowTranslatorを再起動します

### 設定の説明 (Configuration Details)

#### Default プロファイル

汎用的なアプリケーション向けの設定例：

- **タイトルバー** (0.1, 0.05) - 80% x 10%: ウィンドウ上部のタイトルテキスト
- **メニュー** (0.05, 0.15) - 20% x 70%: 左側のメニュー領域
- **ダイアログ** (0.3, 0.4) - 60% x 30%: 中央のダイアログボックス

#### ExampleGame プロファイル

ゲーム向けの設定例：

- **字幕** (0.15, 0.8) - 70% x 15%: 画面下部の字幕領域
- **ステータス** (0.05, 0.05) - 30% x 15%: 左上のステータス表示

### 座標系 (Coordinate System)

すべての座標は相対値（0.0 - 1.0）で指定します：

- X, Y: 矩形の左上角の位置
- Width, Height: 矩形のサイズ

例: X=0.1 は画面幅の10%の位置、Width=0.5は画面幅の50%のサイズ

All coordinates are specified as relative values (0.0 - 1.0):

- X, Y: Position of the top-left corner
- Width, Height: Size of the rectangle

Example: X=0.1 means 10% of screen width, Width=0.5 means 50% of screen width

### カスタマイズ (Customization)

独自の矩形を追加する場合：

1. 対象ウィンドウを表示
2. 認識したい領域の位置とサイズを目測で確認
3. 相対座標に変換（画面幅・高さに対する割合）
4. PriorityRectsリストに追加

To add your own rectangles:

1. Display the target window
2. Visually identify the position and size of the area you want to recognize
3. Convert to relative coordinates (ratio to screen width/height)
4. Add to the PriorityRects list

### 注意事項 (Notes)

- 優先度は配列の順序で決まります（先頭が最優先）
- 矩形が画像範囲外になる場合はスキップされます
- Keywordは翻訳のコンテキストとして使用されます（将来的に翻訳精度向上に活用予定）

- Priority is determined by array order (first item has highest priority)
- Rectangles outside the image bounds will be skipped
- Keywords are used as translation context (planned for future translation accuracy improvements)
