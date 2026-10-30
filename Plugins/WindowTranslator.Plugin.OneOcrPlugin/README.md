# WindowTranslator OneOCR Plugin

## ja

[WindowTranslator](https://github.com/Freeesia/WindowTranslator)で、WindowsのSnipping Toolに含まれるOneOCRエンジンを利用するOCRプラグインです。

## 機能

- WindowsのローカルOCRエンジンによる高速で高精度な文字認識
- OCR領域の結合、傾き、拡大率、明るさ、コントラストを考慮した後処理
- 認識結果から翻訳先言語のテキストを除外

## 段落の結合設定

小さな傾きも保持し、傾きを補正した座標で横方向の断片結合と段落判定を行います。共通の認識設定にある「段落内の行送り許容倍率」（既定値1.45）を小さくすると段落を分けやすく、大きくすると行を結合しやすくなります。同じ列の行送りを推定できない少数行では、既存の「行間のしきい値」を使います。

アイコン入りの行や大小の見出しが分断される場合は、既存の「フォントサイズのしきい値」を上げて調整できます。PR #755のアイコン入り画像で使われた設定値に合わせる場合は、行送り許容倍率を1.90、フォントサイズのしきい値を120%にします。通常の既定値は1.45／25%です。OCRエンジン自体の文字誤認識は、この結合設定では補正されません。

結合処理は、Windows OCR・Tesseract・実験用のGemini/ChatGPT OCRと共通の `OcrUtility.MergeTextRects` を利用します。入力は同じOCR対象範囲の単語または行の `TextRect`、同じ倍率の全体画像サイズ、`BasicOcrParam`、原文言語です。ほかのOCRプラグインでも、認識結果をこの形式に変換して利用できます。

## 必要条件

- OneOCRを含む対応バージョンのWindows Snipping Tool
- 初回設定時に、Snipping Toolから必要なOneOCRコンポーネントをWindowTranslatorの共有データ領域へコピーできること

対応するSnipping Toolが見つからない場合は、WindowTranslatorからMicrosoft Storeを開いて更新できます。OneOCR本体とモデルは、このNuGetパッケージには含まれません。

## 参考

- [SnippingToolOcrSharp](https://github.com/ksasao/SnippingToolOcrSharp)

## en

An OCR plugin for [WindowTranslator](https://github.com/Freeesia/WindowTranslator) that uses the OneOCR engine included with Windows Snipping Tool.

## Features

- Fast, high-accuracy text recognition with a local Windows OCR engine
- Post-processing that accounts for merged OCR regions, rotation, scale, brightness, and contrast
- Excludes text written in the translation target language from recognition results

## Paragraph merging settings

Small rotations are preserved. Fragments and paragraphs are merged in coordinates corrected for the text angle. Lower the common recognition setting "Paragraph line spacing tolerance" (default: 1.45) to split paragraphs more readily, or raise it to join more lines. When too few lines are available to estimate normal line spacing, the existing leading threshold is used.

For lines containing icons or headings of different sizes, increase the existing font size threshold. To use the settings reported for the icon sample in PR #755, set the line spacing tolerance to 1.90 and the font size threshold to 120%. The defaults are 1.45 and 25%. These settings adjust merging, rather than correcting characters misrecognized by the OCR engine.

The plugin calls `OcrUtility.MergeTextRects`, shared with Windows OCR, Tesseract, and the experimental Gemini/ChatGPT OCR modules. It accepts word or line `TextRect` instances from one OCR region, the full image size at the same scale, `BasicOcrParam`, and the source language. Additional OCR plugins can convert their results to this format to use the same merging step.

## Requirements

- A supported version of Windows Snipping Tool that includes OneOCR
- Permission during initial setup to copy the required OneOCR components from Snipping Tool to WindowTranslator's shared data directory

If a supported Snipping Tool cannot be found, WindowTranslator can open Microsoft Store so it can be updated. The OneOCR engine and models are not included in this NuGet package.

## Reference

- [SnippingToolOcrSharp](https://github.com/ksasao/SnippingToolOcrSharp)

## ar

وحدة OCR محلية مقدمة من Microsoft.

### المزايا
- **دقة التعرف**: أعلى دقة في التعرف
- **سريع**: سرعة معالجة عالية جداً

## cs

Místní modul OCR dodaný společností Microsoft.

### Výhody
- **Přesnost rozpoznávání**: Může se pochlubit nejvyšší přesností rozpoznávání
- **Rychlost**: Velmi vysoká rychlost zpracování

## de

Ein lokales OCR-Modul von Microsoft.

### Vorteile
- **Erkennungsgenauigkeit**: Verfügt über die höchste Erkennungsgenauigkeit
- **Schnell**: Sehr hohe Verarbeitungsgeschwindigkeit

## es

Un módulo OCR local proporcionado por Microsoft.

### Ventajas
- **Precisión de reconocimiento**: La más alta precisión de reconocimiento
- **Rápido**: Velocidad de procesamiento muy rápida

## fa

ماژول OCR محلی ارائه‌شده توسط Microsoft.

### مزایا
- **دقت تشخیص**: بالاترین دقت تشخیص
- **سریع**: سرعت پردازش بسیار بالا

## fil

Isang lokal na modyul ng OCR na ibinigay ng Microsoft.

### Mga Bentahe
- **Katumpakan ng Pagkilala**: Nag-aanyaya ng pinakamataas na katumpakan ng pagkilala
- **Mabilis**: Napakabilis ng bilis ng pagproseso

## fr

Un module OCR local fourni par Microsoft.

### Avantages
- **Précision de reconnaissance**: La plus haute précision de reconnaissance
- **Rapide**: Vitesse de traitement très rapide

## hi

Microsoft द्वारा प्रदान किया गया एक स्थानीय OCR मॉड्यूल।

### लाभ
- **पहचान सटीकता**: उच्चतम पहचान सटीकता का दावा करता है
- **तेज़**: बहुत तेज़ प्रोसेसिंग गति

## hu

A Microsoft által biztosított helyi OCR modul.

### Előnyök
- **Felismerési pontosság**: A legjobb felismerési pontossággal rendelkezik
- **Sebesség**: Nagyon gyors feldolgozási sebesség

## id

Modul OCR lokal yang disediakan oleh Microsoft.

### Keuntungan
- **Akurasi Pengenalan**: Memiliki akurasi pengenalan tertinggi
- **Cepat**: Kecepatan pemrosesan sangat cepat

## ko

Microsoft가 제공하는 로컬 OCR 모듈입니다.

### 장점
- **인식 정확도**: 가장 높은 인식 정확도를 자랑합니다
- **빠름**: 처리 속도가 매우 빠릅니다

## ms

Modul OCR tempatan yang disediakan oleh Microsoft.

### Kelebihan
- **Ketepatan Pengecaman**: Mempunyai ketepatan pengecaman tertinggi
- **Pantas**: Kelajuan pemprosesan sangat pantas

## pl

Lokalny moduł OCR dostarczony przez Microsoft.

### Zalety
- **Dokładność rozpoznawania**: Może pochwalić się najwyższą dokładnością rozpoznawania
- **Szybkość**: Bardzo szybka prędkość przetwarzania

## pt-BR

Módulo OCR local fornecido pela Microsoft.

### Vantagens
- **Precisão de reconhecimento**: Possui a maior precisão de reconhecimento
- **Rápido**: Velocidade de processamento muito rápida

## ru

Локальный OCR-модуль, предоставляемый Microsoft.

### Преимущества
- **Точность распознавания**: Обладает наивысшей точностью распознавания
- **Скорость**: Очень высокая скорость обработки

## th

โมดูล OCR ในเครื่องที่จัดทำโดย Microsoft

### ข้อดี
- **ความแม่นยำในการรู้จำ**: มีความแม่นยำในการรู้จำสูงสุด
- **ความเร็ว**: ความเร็วในการประมวลผลสูงมาก

## tr

Microsoft tarafından sağlanan yerel bir OCR modülü.

### Avantajlar
- **Tanıma Doğruluğu**: En yüksek tanıma doğruluğuna sahiptir
- **Hızlı**: Çok hızlı işleme hızı

## vi

Mô-đun OCR cục bộ do Microsoft cung cấp.

### Ưu điểm
- **Độ chính xác nhận dạng**: Có độ chính xác nhận dạng cao nhất
- **Nhanh**: Tốc độ xử lý rất nhanh

## zh-CN

Microsoft 提供的本地 OCR 模块。

### 优点
- **识别准确度**：拥有最高的识别准确度
- **快速**：处理速度非常快

## zh-TW

Microsoft 提供的本地 OCR 模組。

### 優點
- **識別準確度**：擁有最高的識別準確度
- **快速**：處理速度非常快
