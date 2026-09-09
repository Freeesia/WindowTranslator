# WindowTranslator OrcaRouter Plugin

## ja

[WindowTranslator](https://github.com/Freeesia/WindowTranslator)で、OrcaRouterのOpenAI互換APIを利用する翻訳プラグインです。

## 機能

- OAuth 2.0 + PKCEによるブラウザー認証。APIキーや接続先の手入力は不要です。
- 無料の`orcarouter/free`を既定値とし、`orcarouter/auto`または特定モデルも選択可能
- モデルごとの入力・出力料金を100万トークン単位で表示
- カスタム翻訳コンテキストとCSV用語集
- 対応モデルではJSON SchemaによるStructured Outputsを使用し、非対応モデルでは通常のJSON出力へ自動フォールバック
- 応答の検証と一時的な出力エラーからの自動再試行

## 設定

1. プラグインストアからインストールし、WindowTranslatorを再起動します。
2. 対象ごとの設定で「OrcaRouter翻訳」を選択します。
3. 「OrcaRouter設定」の「サインイン / サインアウト」を押し、ブラウザーで認証を許可します。
4. モデルを選択し、設定を保存・適用します。既定値は無料モデルのみへ振り分ける`orcarouter/free`です。

用語集はヘッダーなしの`原文,訳文`形式のCSVファイルです。翻訳テキストはOrcaRouterと、転送先となる上流モデルの提供元（OpenAI、Anthropic、Googleなど）へ送信されます。`orcarouter/auto`と有料モデルにはOrcaRouterのクレジットが必要です。料金、上限、データの扱いは各サービスの契約内容に従います。

認証情報とモデル選択は対象ごとの設定JSONに保存されます。APIキーは設定UIには表示されません。サインアウト後に設定を保存・適用すると、保存されたAPIキーが削除されます。OrcaRouter側でもキーを無効にする場合は、Authorized Appsから連携を取り消してください。

## en

A [WindowTranslator](https://github.com/Freeesia/WindowTranslator) translation plugin that uses OrcaRouter's OpenAI-compatible API.

## Features

- Browser-based OAuth 2.0 + PKCE sign-in with no API key or endpoint entry
- Free-only routing through `orcarouter/free` by default, with `orcarouter/auto` and specific models also available
- Input and output pricing displayed per one million tokens
- Custom translation context and CSV glossaries
- JSON Schema structured outputs on supported models, with automatic fallback to ordinary JSON output on unsupported models
- Response validation and automatic retries for transient output errors

## Configuration

1. Install the plugin from the plugin store and restart WindowTranslator.
2. Select "OrcaRouter Translation" in the settings for the target.
3. Select "Sign in / Sign out" under "OrcaRouter settings" and authorize access in the browser.
4. Select a model, then save and apply the settings. The default is `orcarouter/free`, which only routes to free models.

Glossaries use a headerless CSV file in `source,target` format. Translation text is sent to OrcaRouter and the upstream model provider selected as the destination, such as OpenAI, Anthropic, or Google. `orcarouter/auto` and paid models require OrcaRouter credit. Pricing, usage limits, and data handling depend on the applicable service terms.

Credentials and the selected model are stored in the target's settings JSON. The API key is not shown in the settings UI. Saving and applying the settings after signing out removes the stored API key. To revoke the key on OrcaRouter as well, disconnect the application from Authorized Apps.

## ar

ملحق ترجمة لـ [WindowTranslator](https://github.com/Freeesia/WindowTranslator) يستخدم واجهة OrcaRouter المتوافقة مع OpenAI.

### المزايا

- تسجيل الدخول عبر المتصفح باستخدام OAuth 2.0 + PKCE من دون إدخال مفتاح API يدوياً
- اختيار النموذج تلقائياً عبر `orcarouter/auto` أو تحديد نموذج معين
- عرض أسعار الإدخال والإخراج لكل مليون رمز
- دعم سياق الترجمة ومسارد CSV

ثبّت الملحق من متجر الملحقات، وأعد تشغيل WindowTranslator، ثم اختر ترجمة OrcaRouter وسجّل الدخول من إعداداتها. يُرسل نص الترجمة إلى OrcaRouter ومزوّد النموذج المحدد.

## cs

Překladový plugin pro [WindowTranslator](https://github.com/Freeesia/WindowTranslator), který používá rozhraní OrcaRouter kompatibilní s OpenAI.

### Funkce

- Přihlášení v prohlížeči pomocí OAuth 2.0 + PKCE bez ručního zadávání klíče API
- Automatický výběr modelu přes `orcarouter/auto` nebo výběr konkrétního modelu
- Zobrazení cen vstupu a výstupu za jeden milion tokenů
- Podpora kontextu překladu a slovníků CSV

Nainstalujte plugin z obchodu, restartujte WindowTranslator, vyberte překlad OrcaRouter a přihlaste se v jeho nastavení. Překládaný text se odesílá službě OrcaRouter a poskytovateli vybraného modelu.

## de

Ein Übersetzungsplugin für [WindowTranslator](https://github.com/Freeesia/WindowTranslator), das die OpenAI-kompatible API von OrcaRouter verwendet.

### Funktionen

- Browseranmeldung mit OAuth 2.0 + PKCE ohne manuelle Eingabe eines API-Schlüssels
- Automatische Modellwahl über `orcarouter/auto` oder Auswahl eines bestimmten Modells
- Anzeige der Ein- und Ausgabepreise pro Million Token
- Unterstützung für Übersetzungskontext und CSV-Glossare

Installieren Sie das Plugin aus dem Plugin-Store, starten Sie WindowTranslator neu, wählen Sie OrcaRouter-Übersetzung und melden Sie sich in den Einstellungen an. Der zu übersetzende Text wird an OrcaRouter und den Anbieter des ausgewählten Modells gesendet.

## es

Un plugin de traducción para [WindowTranslator](https://github.com/Freeesia/WindowTranslator) que utiliza la API de OrcaRouter compatible con OpenAI.

### Funciones

- Inicio de sesión en el navegador mediante OAuth 2.0 + PKCE sin introducir manualmente una clave de API
- Selección automática mediante `orcarouter/auto` o elección de un modelo específico
- Precios de entrada y salida por millón de tokens
- Compatibilidad con contexto de traducción y glosarios CSV

Instale el plugin desde la tienda, reinicie WindowTranslator, seleccione la traducción de OrcaRouter e inicie sesión desde sus ajustes. El texto se envía a OrcaRouter y al proveedor del modelo seleccionado.

## fa

افزونه ترجمه برای [WindowTranslator](https://github.com/Freeesia/WindowTranslator) که از API سازگار با OpenAI در OrcaRouter استفاده می‌کند.

### قابلیت‌ها

- ورود از طریق مرورگر با OAuth 2.0 + PKCE بدون وارد کردن دستی کلید API
- انتخاب خودکار مدل با `orcarouter/auto` یا انتخاب یک مدل مشخص
- نمایش هزینه ورودی و خروجی به ازای یک میلیون توکن
- پشتیبانی از زمینه ترجمه و واژه‌نامه CSV

افزونه را از فروشگاه نصب کنید، WindowTranslator را دوباره راه‌اندازی کنید، ترجمه OrcaRouter را انتخاب کرده و از تنظیمات آن وارد شوید. متن ترجمه برای OrcaRouter و ارائه‌دهنده مدل انتخاب‌شده ارسال می‌شود.

## fil

Isang translation plugin para sa [WindowTranslator](https://github.com/Freeesia/WindowTranslator) na gumagamit ng OpenAI-compatible API ng OrcaRouter.

### Mga feature

- Pag-sign in sa browser gamit ang OAuth 2.0 + PKCE nang hindi mano-manong naglalagay ng API key
- Awtomatikong pagpili gamit ang `orcarouter/auto` o pagpili ng partikular na modelo
- Pagpapakita ng presyo ng input at output bawat isang milyong token
- Suporta sa translation context at mga CSV glossary

I-install ang plugin mula sa plugin store, i-restart ang WindowTranslator, piliin ang OrcaRouter Translation, at mag-sign in sa mga setting nito. Ipinapadala ang tekstong isasalin sa OrcaRouter at sa provider ng napiling modelo.

## fr

Un plugin de traduction pour [WindowTranslator](https://github.com/Freeesia/WindowTranslator) qui utilise l'API compatible OpenAI d'OrcaRouter.

### Fonctionnalités

- Connexion dans le navigateur avec OAuth 2.0 + PKCE, sans saisie manuelle de clé API
- Sélection automatique via `orcarouter/auto` ou choix d'un modèle précis
- Affichage des tarifs d'entrée et de sortie par million de jetons
- Prise en charge du contexte de traduction et des glossaires CSV

Installez le plugin depuis la boutique, redémarrez WindowTranslator, sélectionnez la traduction OrcaRouter et connectez-vous dans ses paramètres. Le texte est envoyé à OrcaRouter et au fournisseur du modèle sélectionné.

## hi

[WindowTranslator](https://github.com/Freeesia/WindowTranslator) के लिए अनुवाद प्लगइन, जो OrcaRouter के OpenAI-संगत API का उपयोग करता है।

### विशेषताएँ

- API कुंजी हाथ से दर्ज किए बिना OAuth 2.0 + PKCE द्वारा ब्राउज़र साइन-इन
- `orcarouter/auto` से स्वचालित मॉडल चयन या किसी विशिष्ट मॉडल का चुनाव
- प्रति दस लाख टोकन इनपुट और आउटपुट मूल्य का प्रदर्शन
- अनुवाद संदर्भ और CSV शब्दावली का समर्थन

प्लगइन स्टोर से इसे इंस्टॉल करें, WindowTranslator पुनः आरंभ करें, OrcaRouter अनुवाद चुनें और उसकी सेटिंग से साइन इन करें। अनुवाद का पाठ OrcaRouter और चुने गए मॉडल के प्रदाता को भेजा जाता है।

## hu

A [WindowTranslator](https://github.com/Freeesia/WindowTranslator) fordítási bővítménye, amely az OrcaRouter OpenAI-kompatibilis API-ját használja.

### Funkciók

- Böngészős bejelentkezés OAuth 2.0 + PKCE használatával, API-kulcs kézi megadása nélkül
- Automatikus modellválasztás az `orcarouter/auto` segítségével vagy egy konkrét modell kiválasztása
- Bemeneti és kimeneti árak megjelenítése egymillió tokenenként
- Fordítási kontextus és CSV-szójegyzékek támogatása

Telepítse a bővítményt az áruházból, indítsa újra a WindowTranslatort, válassza az OrcaRouter-fordítást, majd jelentkezzen be a beállításaiban. A fordítandó szöveg az OrcaRouterhez és a kiválasztott modell szolgáltatójához kerül.

## id

Plugin terjemahan untuk [WindowTranslator](https://github.com/Freeesia/WindowTranslator) yang menggunakan API OrcaRouter yang kompatibel dengan OpenAI.

### Fitur

- Masuk melalui browser dengan OAuth 2.0 + PKCE tanpa memasukkan kunci API secara manual
- Pemilihan model otomatis melalui `orcarouter/auto` atau memilih model tertentu
- Menampilkan harga input dan output per satu juta token
- Mendukung konteks terjemahan dan glosarium CSV

Instal plugin dari toko, mulai ulang WindowTranslator, pilih terjemahan OrcaRouter, lalu masuk melalui pengaturannya. Teks terjemahan dikirim ke OrcaRouter dan penyedia model yang dipilih.

## ko

[WindowTranslator](https://github.com/Freeesia/WindowTranslator)에서 OrcaRouter의 OpenAI 호환 API를 사용하는 번역 플러그인입니다.

### 기능

- API 키를 직접 입력하지 않는 OAuth 2.0 + PKCE 브라우저 로그인
- `orcarouter/auto`를 통한 자동 모델 선택 또는 특정 모델 선택
- 백만 토큰당 입력 및 출력 요금 표시
- 번역 컨텍스트와 CSV 용어집 지원

플러그인 스토어에서 설치하고 WindowTranslator를 다시 시작한 뒤 OrcaRouter 번역을 선택하여 설정에서 로그인하세요. 번역 텍스트는 OrcaRouter와 선택한 모델의 제공업체로 전송됩니다.

## ms

Plugin terjemahan untuk [WindowTranslator](https://github.com/Freeesia/WindowTranslator) yang menggunakan API OrcaRouter yang serasi dengan OpenAI.

### Ciri

- Log masuk melalui pelayar dengan OAuth 2.0 + PKCE tanpa memasukkan kunci API secara manual
- Pemilihan model automatik melalui `orcarouter/auto` atau memilih model tertentu
- Paparan harga input dan output bagi setiap satu juta token
- Sokongan konteks terjemahan dan glosari CSV

Pasang plugin daripada kedai, mulakan semula WindowTranslator, pilih terjemahan OrcaRouter dan log masuk melalui tetapannya. Teks terjemahan dihantar kepada OrcaRouter dan penyedia model yang dipilih.

## pl

Plugin tłumaczeniowy dla [WindowTranslator](https://github.com/Freeesia/WindowTranslator) korzystający z interfejsu API OrcaRouter zgodnego z OpenAI.

### Funkcje

- Logowanie w przeglądarce przez OAuth 2.0 + PKCE bez ręcznego wpisywania klucza API
- Automatyczny wybór modelu przez `orcarouter/auto` lub wybór konkretnego modelu
- Wyświetlanie cen wejścia i wyjścia za milion tokenów
- Obsługa kontekstu tłumaczenia i glosariuszy CSV

Zainstaluj plugin ze sklepu, uruchom ponownie WindowTranslator, wybierz tłumaczenie OrcaRouter i zaloguj się w jego ustawieniach. Tłumaczony tekst jest wysyłany do OrcaRouter i dostawcy wybranego modelu.

## pt-BR

Um plugin de tradução para o [WindowTranslator](https://github.com/Freeesia/WindowTranslator) que usa a API compatível com OpenAI do OrcaRouter.

### Recursos

- Login pelo navegador com OAuth 2.0 + PKCE, sem inserir uma chave de API manualmente
- Seleção automática via `orcarouter/auto` ou escolha de um modelo específico
- Exibição dos preços de entrada e saída por milhão de tokens
- Suporte a contexto de tradução e glossários CSV

Instale o plugin pela loja, reinicie o WindowTranslator, selecione a tradução OrcaRouter e faça login nas configurações. O texto é enviado ao OrcaRouter e ao provedor do modelo selecionado.

## ru

Плагин перевода для [WindowTranslator](https://github.com/Freeesia/WindowTranslator), использующий совместимый с OpenAI API OrcaRouter.

### Возможности

- Вход через браузер с OAuth 2.0 + PKCE без ручного ввода ключа API
- Автоматический выбор модели через `orcarouter/auto` или выбор конкретной модели
- Отображение стоимости ввода и вывода за миллион токенов
- Поддержка контекста перевода и CSV-глоссариев

Установите плагин из магазина, перезапустите WindowTranslator, выберите перевод OrcaRouter и войдите в систему через его настройки. Текст отправляется в OrcaRouter и поставщику выбранной модели.

## th

ปลั๊กอินแปลภาษาสำหรับ [WindowTranslator](https://github.com/Freeesia/WindowTranslator) ที่ใช้ API ของ OrcaRouter ซึ่งเข้ากันได้กับ OpenAI

### คุณสมบัติ

- ลงชื่อเข้าใช้ผ่านเบราว์เซอร์ด้วย OAuth 2.0 + PKCE โดยไม่ต้องป้อนคีย์ API ด้วยตนเอง
- เลือกโมเดลอัตโนมัติผ่าน `orcarouter/auto` หรือเลือกโมเดลที่ต้องการ
- แสดงราคาอินพุตและเอาต์พุตต่อหนึ่งล้านโทเค็น
- รองรับบริบทการแปลและอภิธานศัพท์ CSV

ติดตั้งปลั๊กอินจากร้านค้า เริ่ม WindowTranslator ใหม่ เลือกการแปล OrcaRouter แล้วลงชื่อเข้าใช้จากการตั้งค่า ข้อความจะถูกส่งไปยัง OrcaRouter และผู้ให้บริการโมเดลที่เลือก

## tr

OrcaRouter'ın OpenAI uyumlu API'sini kullanan bir [WindowTranslator](https://github.com/Freeesia/WindowTranslator) çeviri eklentisi.

### Özellikler

- API anahtarını elle girmeden OAuth 2.0 + PKCE ile tarayıcı üzerinden oturum açma
- `orcarouter/auto` ile otomatik model seçimi veya belirli bir model seçme
- Milyon token başına giriş ve çıkış fiyatlarını gösterme
- Çeviri bağlamı ve CSV sözlük desteği

Eklentiyi mağazadan yükleyin, WindowTranslator'ı yeniden başlatın, OrcaRouter çevirisini seçin ve ayarlarından oturum açın. Çeviri metni OrcaRouter'a ve seçilen modelin sağlayıcısına gönderilir.

## vi

Plugin dịch thuật cho [WindowTranslator](https://github.com/Freeesia/WindowTranslator) sử dụng API tương thích OpenAI của OrcaRouter.

### Tính năng

- Đăng nhập trong trình duyệt bằng OAuth 2.0 + PKCE mà không cần nhập khóa API thủ công
- Tự động chọn mô hình qua `orcarouter/auto` hoặc chọn một mô hình cụ thể
- Hiển thị giá đầu vào và đầu ra cho mỗi một triệu token
- Hỗ trợ ngữ cảnh dịch và bảng thuật ngữ CSV

Cài đặt plugin từ cửa hàng, khởi động lại WindowTranslator, chọn bản dịch OrcaRouter và đăng nhập trong phần cài đặt. Văn bản được gửi đến OrcaRouter và nhà cung cấp mô hình đã chọn.

## zh-CN

一个使用 OrcaRouter OpenAI 兼容 API 的 [WindowTranslator](https://github.com/Freeesia/WindowTranslator) 翻译插件。

### 功能

- 使用 OAuth 2.0 + PKCE 通过浏览器登录，无需手动输入 API 密钥
- 通过 `orcarouter/auto` 自动选择模型，或选择指定模型
- 显示每百万令牌的输入和输出价格
- 支持翻译上下文和 CSV 术语表

从插件商店安装并重新启动 WindowTranslator，选择 OrcaRouter 翻译，然后在设置中登录。翻译文本会发送到 OrcaRouter 和所选模型的提供商。

## zh-TW

一個使用 OrcaRouter OpenAI 相容 API 的 [WindowTranslator](https://github.com/Freeesia/WindowTranslator) 翻譯外掛程式。

### 功能

- 使用 OAuth 2.0 + PKCE 透過瀏覽器登入，無須手動輸入 API 金鑰
- 透過 `orcarouter/auto` 自動選擇模型，或選擇指定模型
- 顯示每百萬權杖的輸入與輸出價格
- 支援翻譯內容脈絡和 CSV 術語表

從外掛程式商店安裝並重新啟動 WindowTranslator，選擇 OrcaRouter 翻譯，然後在設定中登入。翻譯文字會傳送至 OrcaRouter 和所選模型的提供商。
