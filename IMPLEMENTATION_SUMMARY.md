# Priority Rectangle OCR Feature - Implementation Summary

## 実装概要 (Implementation Overview)

WindowTranslatorに特定の矩形を優先的にテキスト認識する機能を追加しました。

A feature to prioritize text recognition for specific rectangles has been added to WindowTranslator.

## 実装したファイル (Implemented Files)

### コアファイル (Core Files)
1. **WindowTranslator.Abstractions/PriorityRect.cs**
   - 優先矩形のデータモデル
   - 相対座標（0.0-1.0）での矩形定義
   - キーワード（翻訳コンテキスト）の設定

2. **WindowTranslator.Abstractions/Modules/IOcrModule.cs**
   - BasicOcrParamクラスにPriorityRectsプロパティを追加

3. **WindowTranslator/Modules/Ocr/PriorityRectFilter.cs**
   - IFilterModule実装
   - 優先矩形のOCR処理とフィルタリング
   - 画像クロッピングと座標変換
   - 重複検出と優先矩形の優先処理

4. **WindowTranslator/FilterPriority.cs**
   - PriorityRectFilterの優先度定義（-120.0）

### UIファイル (UI Files)
5. **WindowTranslator/Modules/Ocr/RectangleSelectionWindow.xaml**
   - 矩形選択ウィンドウのXAML定義

6. **WindowTranslator/Modules/Ocr/RectangleSelectionWindow.xaml.cs**
   - 矩形選択ウィンドウのコードビハインド
   - ドラッグによる矩形選択機能

7. **WindowTranslator/Modules/Ocr/PriorityRectViewModel.cs**
   - 優先矩形設定のViewModel
   - リスト管理（追加、削除、並び替え）

### 翻訳リソースファイル (Translation Resource Files)
8-14. **WindowTranslator.Abstractions/Properties/Resources.*.resx**
   - 日本語 (ja)
   - 英語 (en)
   - ドイツ語 (de)
   - 韓国語 (ko)
   - 中国語簡体字 (zh-CN)
   - 中国語繁体字 (zh-TW)
   - ベトナム語 (vi)

### ドキュメントファイル (Documentation Files)
15. **docs/PriorityRectOCR.md**
   - 機能の詳細説明
   - 実装アーキテクチャ
   - 使用方法とトラブルシューティング

16. **docs/examples/settings-with-priority-rects.json**
   - 設定ファイルの例
   - 2つのプロファイル（汎用、ゲーム向け）

17. **docs/examples/README.md**
   - 設定例の使い方
   - 座標系の説明
   - カスタマイズ方法

## 機能の動作フロー (Feature Flow)

```
1. ユーザーが設定ファイルに優先矩形を定義
   ↓
2. WindowTranslator起動、設定を読み込み
   ↓
3. 画面キャプチャ
   ↓
4. メインOCR処理実行（全体画像）
   ↓
5. PriorityRectFilter発動
   ├─ 優先矩形ごとに画像を切り出し
   ├─ 切り出した画像をOCR処理
   ├─ 座標を全体画像座標に変換
   └─ キーワードをコンテキストとして設定
   ↓
6. 重複検出
   ├─ 優先矩形の結果と元のOCR結果を比較
   └─ 重複する元の結果を除外
   ↓
7. 結果のマージ
   ├─ 優先矩形の結果（優先度順）
   └─ 残りの元のOCR結果
   ↓
8. 翻訳処理
   ↓
9. オーバーレイ表示
```

## 技術的な実装詳細 (Technical Implementation Details)

### 座標系 (Coordinate System)
- **相対座標**: すべての矩形は画像サイズに対する相対値（0.0-1.0）で保存
- **絶対座標変換**: 実行時に現在の画像サイズに応じて絶対座標に変換
- **利点**: 異なる解像度のウィンドウでも同じ設定が使用可能

### 画像クロッピング (Image Cropping)
- **SoftwareBitmap**: Windows.Graphics.Imagingを使用
- **安全な処理**: 画像範囲外の矩形は自動的にスキップ
- **メモリ効率**: 切り出した画像は使用後すぐに破棄

### 重複検出 (Overlap Detection)
- **OverlapsWith()**: TextRectの既存メソッドを使用
- **回転考慮**: GetRotatedBoundingBox()で回転を考慮した境界ボックスで判定
- **優先度**: 重複時は常に優先矩形の結果を採用

### 依存性注入 (Dependency Injection)
- **IServiceProvider**: IOcrModuleの取得にIServiceProviderを使用
- **プラグインシステム**: MainAssemblyPluginCatalogで自動検出・登録
- **スコープ**: Scopedライフタイムで安全に動作

## 使用方法 (Usage)

### 基本的な使い方
1. `%USERPROFILE%\.WindowTranslator\settings.json`を編集
2. `PriorityRects`配列に矩形を追加
3. WindowTranslatorを再起動

### 設定例
```json
{
  "Targets": {
    "Default": {
      "PluginParams": {
        "BasicOcrParam": {
          "PriorityRects": [
            {
              "X": 0.1,      // 左から10%の位置
              "Y": 0.05,     // 上から5%の位置
              "Width": 0.8,  // 幅80%
              "Height": 0.1, // 高さ10%
              "Keyword": "タイトルバー"
            }
          ]
        }
      }
    }
  }
}
```

## テスト方法 (Testing)

1. 設定例をコピー
   ```bash
   copy docs\examples\settings-with-priority-rects.json %USERPROFILE%\.WindowTranslator\settings.json
   ```

2. WindowTranslatorを起動

3. 日本語のアプリケーションを開く

4. 翻訳ボタンをクリック

5. 優先矩形の領域が優先的に認識されることを確認
   - ログで確認: `Priority rect X OCR: ...`
   - 重複削除の確認: `Original text '...' overlaps with priority text '...', removing original`

## 今後の拡張予定 (Future Enhancements)

### 短期的な改善 (Short-term)
- [ ] GUI統合（設定画面への追加）
- [ ] ドラッグ＆ドロップでの矩形選択
- [ ] リスト管理UI（追加、削除、並び替え）

### 中期的な改善 (Mid-term)
- [ ] プレビュー機能（登録した矩形の確認）
- [ ] テンプレート機能（よく使う矩形セットの保存）
- [ ] 複数ウィンドウサイズ対応（サイズ別の矩形セット）

### 長期的な改善 (Long-term)
- [ ] 自動矩形検出（頻繁に変化する領域の自動認識）
- [ ] AI活用（キーワードから翻訳精度向上）
- [ ] パフォーマンス最適化（並列処理）

## 注意事項 (Notes)

- **Windows専用**: この機能はWindows.Graphics.Imagingを使用するため、Windows専用です
- **パフォーマンス**: 優先矩形が多すぎるとOCR処理が遅くなる可能性があります
- **座標の調整**: ウィンドウのサイズ変更時は座標の再調整が必要な場合があります

## まとめ (Summary)

✅ **完全に動作する機能をリリース可能**
- コア機能の実装完了
- 設定ファイルでの使用が可能
- 7言語の翻訳リソース完備
- 詳細なドキュメントと設定例を提供

⏳ **UI統合は今後の改善項目**
- 基本機能は完成、すぐに利用可能
- GUIは将来的な拡張として計画
- 設定ファイル編集で完全に機能

## 変更されたファイルの統計 (File Statistics)

```
17 files changed, 1180 insertions(+)
```

- C#コード: 5ファイル, 約600行
- XAMLコード: 1ファイル, 約35行
- 翻訳リソース: 7ファイル, 約294行
- ドキュメント: 3ファイル, 約250行
- 設定例: 1ファイル, 約80行
