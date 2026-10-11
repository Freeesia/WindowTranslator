# WindowTranslator LLM Plugin

## ja

[WindowTranslator](https://github.com/Freeesia/WindowTranslator)でOpenAI互換APIを利用する多機能プラグインです。

## 機能

- 大規模言語モデルによる文脈を考慮した高品質な翻訳
- 画像対応モデルを利用したAI OCR
- OCRテキストまたは元画像を使った認識結果の補正
- OpenAI APIと互換エンドポイントの両方に対応
- カスタム翻訳コンテキスト、補正サンプル、CSV用語集

AI OCRとOCR補正は実験的な機能です。

## 必要条件と設定

- 利用するモデル名
- サービスが要求するAPIキー
- OpenAI以外を利用する場合はOpenAI互換APIのエンドポイント
- 用語集を利用する場合は、ヘッダーなしの`原文,訳文`形式のCSVファイル

画像やテキストは設定したAPIへ送信されます。料金、上限、データの扱いは利用するサービスの契約内容に従います。

## en

A multi-purpose [WindowTranslator](https://github.com/Freeesia/WindowTranslator) plugin for OpenAI-compatible APIs.

## Features

- High-quality, context-aware translation with large language models
- AI OCR using vision-capable models
- Correction of recognition results using OCR text or the original image
- Supports both the OpenAI API and compatible endpoints
- Custom translation context, correction examples, and CSV glossaries

AI OCR and OCR correction are experimental features.

## Requirements and configuration

- The model name to use
- An API key when required by the service
- An OpenAI-compatible endpoint when using another provider
- A headerless CSV glossary in `source,target` format when terminology control is needed

Images and text are sent to the configured API. Pricing, usage limits, and data handling depend on the selected service.

## ar

وحدة ترجمة تستخدم ChatGPT API أو LLM محلي.

### المزايا
- **أعلى دقة**: ترجمات عالية الجودة بواسطة نماذج اللغة الكبيرة
- **المرونة**: تخصيص المطالبات لتعديل أسلوب الترجمة
- **دعم المسرد**: حافظ على اتساق الترجمة مع المسارد
- **دعم LLM محلي**: إمكانية استخدام خادم LLM الخاص بك

## cs

Překladový modul využívající ChatGPT od OpenAI.

### Výhody
- **Vysoká přesnost překladu**: Vysoká kvalita překladů díky AI
- **Přirozený překlad**: Překlady znějí přirozeně

## de

Ein Übersetzungsmodul, das ChatGPT API oder lokales LLM verwendet.

### Vorteile
- **Höchste Genauigkeit**: Hochwertige Übersetzung durch große Sprachmodelle
- **Flexibilität**: Kann Prompts anpassen, um Übersetzungsstil zu justieren
- **Glossar-Unterstützung**: Kann Übersetzungskonsistenz durch Glossare aufrechterhalten
- **Lokale LLM-Unterstützung**: Kann auch eigenen LLM-Server verwenden

## es

Un módulo de traducción que utiliza la API de ChatGPT o un LLM local.

### Ventajas
- **Mayor precisión**: Traducciones de alta calidad por grandes modelos de lenguaje
- **Flexibilidad**: Personalice prompts para ajustar el estilo de traducción
- **Soporte de glosario**: Mantenga la consistencia de traducción con glosarios
- **Soporte LLM local**: Posibilidad de usar su propio servidor LLM

## fa

ماژول ترجمه‌ای که از ChatGPT API یا LLM محلی استفاده می‌کند.

### مزایا
- **بالاترین دقت**: ترجمه‌های با کیفیت بالا توسط مدل‌های زبانی بزرگ
- **انعطاف‌پذیری**: سفارشی‌سازی دستورالعمل برای تنظیم سبک ترجمه
- **پشتیبانی از واژه‌نامه**: حفظ ثبات ترجمه با واژه‌نامه‌ها
- **پشتیبانی از LLM محلی**: امکان استفاده از سرور LLM خودتان

## fil

Isang modyul ng pagsasalin na gumagamit ng ChatGPT API o local LLM.

### Mga Bentahe
- **Pinakamataas na Katumpakan**: Mataas na kalidad ng pagsasalin ng malalaking modelo ng wika
- **Kakayahang umangkop**: Maaaring i-customize ang mga prompt upang ayusin ang estilo ng pagsasalin
- **Suporta sa Glossary**: Maaaring mapanatili ang consistency ng pagsasalin gamit ang mga glossary
- **Suporta sa Local LLM**: Maaari ring gamitin ang sarili mong LLM server

## fr

Un module de traduction utilisant l'API ChatGPT ou un LLM local.

### Avantages
- **Plus haute précision**: Traductions de haute qualité par grands modèles de langage
- **Flexibilité**: Personnalisez les prompts pour ajuster le style de traduction
- **Support de glossaire**: Maintenez la cohérence de traduction avec des glossaires
- **Support LLM local**: Possibilité d'utiliser votre propre serveur LLM

## hi

ChatGPT API या स्थानीय LLM का उपयोग करने वाला अनुवाद मॉड्यूल।

### फायदे
- **सर्वोच्च सटीकता**: बड़े भाषा मॉडल द्वारा उच्च गुणवत्ता अनुवाद
- **लचीलापन**: अनुवाद शैली को समायोजित करने के लिए प्रॉम्प्ट को कस्टमाइज़ कर सकते हैं
- **शब्दावली समर्थन**: शब्दावली का उपयोग करके अनुवाद की स्थिरता बनाए रख सकते हैं
- **स्थानीय LLM समर्थन**: अपना LLM सर्वर भी उपयोग कर सकते हैं

## hu

Az OpenAI ChatGPT-jét használó fordítási modul.

### Előnyök
- **Magas fordítási pontosság**: Magas minőségű fordítások AI segítségével
- **Természetes fordítás**: A fordítások természetesen hangzanak

## id

Modul terjemahan menggunakan API ChatGPT atau LLM lokal.

### Keuntungan
- **Akurasi Tertinggi**: Terjemahan berkualitas tinggi oleh model bahasa besar
- **Fleksibilitas**: Dapat menyesuaikan prompt untuk menyesuaikan gaya terjemahan
- **Dukungan Glosarium**: Dapat mempertahankan konsistensi terjemahan menggunakan glosarium
- **Dukungan LLM Lokal**: Juga dapat menggunakan server LLM Anda sendiri

## ko

ChatGPT API 또는 로컬 LLM을 사용하는 번역 모듈입니다.

### 장점
- **최고 정확도**: 대규모 언어 모델에 의한 고품질 번역
- **유연성**: 프롬프트를 커스터마이즈하여 번역 스타일을 조정할 수 있습니다
- **용어집 지원**: 용어집을 이용하여 번역의 일관성을 유지할 수 있습니다
- **로컬 LLM 지원**: 자체 LLM 서버도 사용 가능

## ms

Modul terjemahan menggunakan API ChatGPT atau LLM tempatan.

### Kelebihan
- **Ketepatan Tertinggi**: Terjemahan berkualiti tinggi oleh model bahasa besar
- **Fleksibiliti**: Boleh menyesuaikan prompt untuk melaraskan gaya terjemahan
- **Sokongan Glosari**: Boleh mengekalkan konsistensi terjemahan menggunakan glosari
- **Sokongan LLM Tempatan**: Juga boleh menggunakan pelayan LLM anda sendiri

## pl

Moduł tłumaczeniowy wykorzystujący ChatGPT od OpenAI.

### Zalety
- **Wysoka dokładność tłumaczenia**: Wysoka jakość tłumaczeń dzięki AI
- **Naturalne tłumaczenie**: Tłumaczenia brzmią naturalnie

## pt-BR

Módulo de tradução que utiliza ChatGPT API ou LLM local.

### Vantagens
- **Máxima precisão**: Traduções de alta qualidade por modelos de linguagem em grande escala
- **Flexibilidade**: Customize prompts para ajustar o estilo de tradução
- **Suporte a glossário**: Use glossários para manter consistência na tradução
- **Suporte a LLM local**: Também pode usar seu próprio servidor LLM

## ru

Модуль перевода, использующий ChatGPT API или локальный LLM.

### Преимущества
- **Наивысшая точность**: Высококачественный перевод большими языковыми моделями
- **Гибкость**: Можно настраивать подсказки для настройки стиля перевода
- **Поддержка глоссария**: Может поддерживать согласованность перевода с использованием глоссариев
- **Поддержка локального LLM**: Также можно использовать собственный сервер LLM

## th

โมดูลการแปลที่ใช้ ChatGPT API หรือ LLM ในเครื่อง

### ข้อดี
- **ความแม่นยำสูงสุด**: การแปลคุณภาพสูงโดยโมเดลภาษาขนาดใหญ่
- **ความยืดหยุ่น**: สามารถปรับแต่ง prompt เพื่อปรับสไตล์การแปล
- **รองรับอภิธานศัพท์**: สามารถรักษาความสอดคล้องของการแปลโดยใช้อภิธานศัพท์
- **รองรับ LLM ในเครื่อง**: สามารถใช้เซิร์ฟเวอร์ LLM ของคุณเองได้

## tr

ChatGPT API veya yerel LLM kullanan bir çeviri modülü.

### Avantajlar
- **En Yüksek Doğruluk**: Büyük dil modelleri tarafından yüksek kaliteli çeviri
- **Esneklik**: Çeviri stilini ayarlamak için istemler özelleştirebilir
- **Sözlük Desteği**: Sözlükler kullanarak çeviri tutarlılığını koruyabilir
- **Yerel LLM Desteği**: Kendi LLM sunucunuzu da kullanabilirsiniz

## vi

Mô-đun dịch thuật sử dụng ChatGPT API hoặc LLM cục bộ.

### Ưu điểm
- **Độ chính xác cao nhất**: Bản dịch chất lượng cao bằng các mô hình ngôn ngữ lớn
- **Linh hoạt**: Có thể tùy chỉnh prompt để điều chỉnh phong cách dịch
- **Hỗ trợ thuật ngữ**: Có thể duy trì tính nhất quán trong dịch thuật bằng cách sử dụng thuật ngữ
- **Hỗ trợ LLM cục bộ**: Cũng có thể sử dụng máy chủ LLM của riêng bạn

## zh-CN

使用 ChatGPT API 或本地 LLM 的翻译模块。

### 优点
- **最高准确度**：通过大型语言模型实现高质量翻译
- **灵活性**：可以自定义提示来调整翻译风格
- **术语表支持**：可以使用术语表保持翻译一致性
- **本地 LLM 支持**：也可以使用自己的 LLM 服务器

## zh-TW

使用 ChatGPT API 或本地 LLM 的翻譯模組。

### 優點
- **最高準確度**：透過大型語言模型實現高品質翻譯
- **靈活性**：可以自訂提示來調整翻譯風格
- **術語表支援**：可以使用術語表保持翻譯一致性
- **本地 LLM 支援**：也可以使用自己的 LLM 伺服器
