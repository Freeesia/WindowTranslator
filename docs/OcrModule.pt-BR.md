# OCRMódulo

WindowTranslatorでは、画面上のテキストを認識するために複数のOCRMódulode選択できます。  
各Móduloには特徴があり、用途に応じて適切なMóduloを選択することで、より正確なテキスト認識が可能になります。

## 新Windows文字認識(ベータ) ![Padrão](https://img.shields.io/badge/Padrão-brightgreen)

Microsoftが提供するローカルOCRMóduloです。

### Vantagens
- **認識精度**: 最も高い認識精度を誇ります
- **高速**: 処理速度が非常に速いです

### デVantagens
- **使用メモリ**: 認識処理だけで1GB以上のメモリを使用する場合があります
- **動作環境**: 一部環境では動作しない場合があります（Windows 10以降推奨）

---

## Windows標準文字認識

Windows 10以降に標準搭載されているOCRエンジンです。

### Vantagens
- **使用メモリ**: 軽量でメモリ使用量が少ないです
- **動作環境**: Windows 10以降であれば広く利用可能です

### デVantagens
- **認識精度**: 複雑なフォントや手書き文字には弱い場合があります
- **セットアップ**: 言語データの手動インストールが必要な場合があります

---

## Tesseract OCR

オープンソースのOCRエンジンです。

### Vantagens
- **多言語対応**: 100以上の言語に対応しています
- **安定性**: 長い歴史を持つ信頼性の高いエンジン

### デVantagens
- **認識精度**: 他のOCRと比較すると精度が劣る場合があります

---

## Móduloの選び方

認識精度が高い以下の順に動作するMóduloを選択してください。

1. 新Windows文字認識(ベータ)
2. Windows標準文字認識
3. Tesseract OCR
