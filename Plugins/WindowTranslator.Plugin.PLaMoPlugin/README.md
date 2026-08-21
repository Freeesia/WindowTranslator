# WindowTranslator PLaMo Translator Plugin

## ja

[WindowTranslator](https://github.com/Freeesia/WindowTranslator)で、日本語に強いPLaMo 2 Translateモデルをローカル実行する翻訳プラグインです。

## 機能

- LLamaSharpを使用した日本語に強いローカル翻訳
- 翻訳テキストを外部サービスへ送信せずに処理
- 初回利用時に量子化済みPLaMo翻訳モデルを自動取得
- コンテキスト長と使用するVRAM量を設定可能

## 必要条件

- 64ビット版Windows
- 十分な空きストレージとメモリ
- CUDAに対応するNVIDIA GPUとドライバーを推奨
- モデルを初めて取得するときのインターネット接続

モデル取得後の翻訳はローカルで実行されます。用語集と追加コンテキストには対応していません。

## en

A [WindowTranslator](https://github.com/Freeesia/WindowTranslator) translation plugin that runs the Japanese-focused PLaMo 2 Translate model locally.

## Features

- Japanese-focused local translation through LLamaSharp
- Processes translated text without sending it to an external service
- Downloads a quantized PLaMo translation model when it is first used
- Configurable context length and VRAM usage

## Requirements

- 64-bit Windows
- Sufficient free storage and memory
- An NVIDIA GPU and driver with CUDA support are recommended
- An internet connection when downloading the model for the first time

After the model has been downloaded, translation runs locally. Glossaries and additional translation context are not supported.

## ar

وحدة ترجمة تستخدم LLM محلي متخصص في اليابانية.

### المزايا
- **متخصص في اليابانية**: محسن للترجمات اليابانية
- **مجاني تماماً**: نموذج مفتوح المصدر بدون رسوم
- **الخصوصية**: يعمل محلياً، البيانات لا تُرسل للخارج
- **غير متصل**: لا حاجة لاتصال بالإنترنت

## cs

Překlad PLaMo

- Délka kontextu: Větší hodnoty umožňují překládat delší texty.
- Využití VRAM: Množství paměti GPU k použití. -1: pouze GPU. 0: pouze CPU. (Jednotka: GB)

## de

Ein Übersetzungsmodul, das lokales LLM spezialisiert für Japanisch verwendet.

### Vorteile
- **Japanisch-spezialisiert**: Optimiert für japanische Übersetzung
- **Völlig kostenlos**: Open-Source-Modell ohne Gebühren
- **Datenschutz**: Läuft lokal, Daten werden nicht extern gesendet
- **Offline**: Keine Internetverbindung erforderlich

## es

Un módulo de traducción que utiliza un LLM local especializado para japonés.

### Ventajas
- **Especializado en japonés**: Optimizado para traducciones al japonés
- **Completamente gratis**: Modelo de código abierto sin cargos
- **Privacidad**: Funciona localmente, los datos no se envían al exterior
- **Sin conexión**: No se necesita conexión a Internet

## fa

ماژول ترجمه‌ای که از LLM محلی تخصصی در زبان ژاپنی استفاده می‌کند.

### مزایا
- **تخصصی در ژاپنی**: بهینه‌شده برای ترجمه‌های ژاپنی
- **کاملاً رایگان**: مدل متن‌باز بدون هزینه
- **حریم خصوصی**: به صورت محلی اجرا می‌شود، داده‌ها به خارج ارسال نمی‌شوند
- **آفلاین**: نیاز به اتصال به اینترنت ندارد

## fil

Isang modyul ng pagsasalin na gumagamit ng local LLM na dalubhasa para sa wikang Hapon.

### Mga Bentahe
- **Dalubhasa sa Hapon**: Naka-optimize para sa pagsasalin ng Hapon
- **Lubos na Libre**: Open source model na walang bayad
- **Privacy**: Tumatakbo nang lokal, ang data ay hindi ipinapadala sa labas
- **Offline**: Walang kailangang koneksyon sa internet

## fr

Un module de traduction utilisant un LLM local spécialisé pour le japonais.

### Avantages
- **Spécialisé japonais**: Optimisé pour les traductions japonaises
- **Complètement gratuit**: Modèle open source sans frais
- **Confidentialité**: Fonctionne localement, les données ne sont pas envoyées à l'extérieur
- **Hors ligne**: Pas de connexion Internet nécessaire

## hi

जापानी भाषा के लिए विशेष स्थानीय LLM का उपयोग करने वाला अनुवाद मॉड्यूल।

### फायदे
- **जापानी विशेषज्ञता**: जापानी अनुवाद के लिए अनुकूलित
- **पूर्ण रूप से निःशुल्क**: ओपन सोर्स मॉडल के साथ कोई शुल्क नहीं
- **गोपनीयता**: स्थानीय रूप से चलता है, डेटा बाहर नहीं भेजा जाता
- **ऑफ़लाइन**: इंटरनेट कनेक्शन की आवश्यकता नहीं

## hu

PLaMo fordítás

- Kontextus hossza: A nagyobb értékek lehetővé teszik hosszabb szövegek fordítását.
- VRAM használat: A használandó GPU memória mennyisége. -1: Csak GPU. 0: Csak CPU. (Egység: GB)

## id

Modul terjemahan menggunakan LLM lokal khusus untuk Bahasa Jepang.

### Keuntungan
- **Khusus Jepang**: Dioptimalkan untuk terjemahan Jepang
- **Sepenuhnya Gratis**: Model sumber terbuka tanpa biaya
- **Privasi**: Berjalan secara lokal, data tidak dikirim ke luar
- **Offline**: Tidak ada koneksi internet yang diperlukan

## ko

일본어에 특화된 로컬 LLM을 사용하는 번역 모듈입니다.

### 장점
- **일본어 특화**: 일본어 번역에 최적화되어 있습니다
- **완전 무료**: 오픈 소스 모델로 비용이 발생하지 않습니다
- **프라이버시**: 로컬에서 작동하므로 데이터가 외부로 전송되지 않습니다
- **오프라인**: 인터넷 연결이 필요 없습니다

## ms

Modul terjemahan menggunakan LLM tempatan khusus untuk Bahasa Jepun.

### Kelebihan
- **Khusus Jepun**: Dioptimumkan untuk terjemahan Jepun
- **Percuma Sepenuhnya**: Model sumber terbuka tanpa caj
- **Privasi**: Berjalan secara tempatan, data tidak dihantar ke luar
- **Luar Talian**: Tiada sambungan internet diperlukan

## pl

Tłumaczenie PLaMo

- Długość kontekstu: Większe wartości umożliwiają tłumaczenie dłuższych tekstów.
- Użycie VRAM: Ilość pamięci GPU do użycia. -1: tylko GPU. 0: tylko CPU. (Jednostka: GB)

## pt-BR

Módulo de tradução que utiliza LLM local especializado em japonês.

### Vantagens
- **Especializado em japonês**: Otimizado para tradução de japonês
- **Totalmente gratuito**: Modelo de código aberto sem custos
- **Privacidade**: Como opera localmente, dados não são enviados externamente
- **Offline**: Não requer conexão à internet

## ru

Модуль перевода, использующий локальный LLM, специализированный для японского языка.

### Преимущества
- **Специализация на японском**: Оптимизирован для перевода на японский язык
- **Полностью бесплатно**: Модель с открытым исходным кодом без платежей
- **Конфиденциальность**: Работает локально, данные не передаются наружу
- **Автономный**: Не требуется подключение к интернету

## th

โมดูลการแปลที่ใช้ LLM ในเครื่องที่เชี่ยวชาญสำหรับภาษาญี่ปุ่น

### ข้อดี
- **เชี่ยวชาญภาษาญี่ปุ่น**: ปรับให้เหมาะสมสำหรับการแปลภาษาญี่ปุ่น
- **ฟรีทั้งหมด**: โมเดลโอเพนซอร์สไม่มีค่าใช้จ่าย
- **ความเป็นส่วนตัว**: ทำงานในเครื่อง ข้อมูลไม่ถูกส่งออกภายนอก
- **ออฟไลน์**: ไม่ต้องการการเชื่อมต่ออินเทอร์เน็ต

## tr

Japonca için özelleştirilmiş yerel LLM kullanan bir çeviri modülü.

### Avantajlar
- **Japonca Uzmanlaşması**: Japonca çeviri için optimize edilmiş
- **Tamamen Ücretsiz**: Açık kaynak modeli, ücret yok
- **Gizlilik**: Yerel olarak çalışır, veriler dışarıya gönderilmez
- **Çevrimdışı**: İnternet bağlantısı gerekmez

## vi

Mô-đun dịch thuật sử dụng LLM cục bộ chuyên về tiếng Nhật.

### Ưu điểm
- **Chuyên về tiếng Nhật**: Được tối ưu hóa cho dịch tiếng Nhật
- **Hoàn toàn miễn phí**: Mô hình nguồn mở không tốn phí
- **Quyền riêng tư**: Chạy cục bộ, dữ liệu không được gửi ra bên ngoài
- **Ngoại tuyến**: Không cần kết nối internet

## zh-CN

使用专门针对日语的本地 LLM 的翻译模块。

### 优点
- **日语专用**：针对日语翻译进行了优化
- **完全免费**：开源模型不产生费用
- **隐私保护**：在本地运行，数据不会发送到外部
- **离线**：无需互联网连接

## zh-TW

使用專門針對日語的本地 LLM 的翻譯模組。

### 優點
- **日語專用**：針對日語翻譯進行了最佳化
- **完全免費**：開源模型不產生費用
- **隱私保護**：在本地執行，資料不會傳送到外部
- **離線**：無需網際網路連線
