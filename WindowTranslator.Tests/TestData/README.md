# OCR段落結合の回帰データ

`OcrParagraphs.json` は [Issue #770](https://github.com/Freeesia/WindowTranslator/issues/770) の新聞と、[Issue #754](https://github.com/Freeesia/WindowTranslator/issues/754) / [PR #755](https://github.com/Freeesia/WindowTranslator/pull/755) の手紙3枚・アイコン入り説明の、OneOCR認識行を固定したもの。

- 元画像は各1920×1080。Microsoft.ScreenSketch 11.2608.41.0のOneOCRを、倍率1・明るさ0・コントラスト0で実行して取得した。
- `Lines` は認識された153行の座標・傾き・文字サイズ・単語数と、行方向へ投影した単語高の中央値。原文の代わりに、テスト側で同じ単語数の識別子を使う。`Symbols` は記号だけの単語の位置で、テストでは別の記号 `◇` に置き換える。
- `Paragraphs` は目視確認した本文105行の19領域を、0始まりの行番号で読み順に指定する。新聞の別の列は別の領域とする。
- 見出し、挨拶、手書きの結語、背面の紙、UIは本文の正解数に含めない。本文に混入した場合は不一致になる。
- OCRエンジンを起動するテストではなく、保存した認識結果に対する製品の結合処理のテスト。倍率0.5・1・2と、画像全体の回転-7・0・7度を組み合わせて確認する。

実際の認識・文字数と数字記号の除外・ゲームや外部アプリのUIの評価は、この本文用データだけでは保証しない。
