# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/fa-ir%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator یک ابزار ترجمه پنجره برنامه‌ها در ویندوز است.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md) | [PT-BR](./README.pt-BR.md) | [FR](./README.fr.md) | [ES](./README.es.md) | [AR](./README.ar.md) | [TR](./README.tr.md) | [TH](./README.th.md) | [RU](./README.ru.md) | [FIL](./README.fil.md) | [PL](./README.pl.md) | [FA](./README.fa.md)

## فهرست مطالب
- [ WindowTranslator](#-windowtranslator)
  - [فهرست مطالب](#فهرست-مطالب)
  - [دانلود](#دانلود)
    - [نسخه Microsoft Store ](#نسخه-microsoft-store-)
    - [نسخه نصب‌کننده](#نسخه-نصبکننده)
    - [نسخه قابل حمل](#نسخه-قابل-حمل)
  - [نحوه استفاده](#نحوه-استفاده)
    - [Bergamot ](#bergamot-)
  - [ویژگی‌های دیگر](#ویژگیهای-دیگر)

## دانلود
### نسخه Microsoft Store ![توصیه‌شده](https://img.shields.io/badge/توصیه‌شده-brightgreen)

از [Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct) نصب کنید.
حتی در محیط‌هایی که .NET نصب نشده است کار می‌کند.

### نسخه نصب‌کننده

`WindowTranslator-(نسخه).msi` را از [صفحه انتشار GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) دانلود کرده و برای نصب اجرا کنید.  
ویدیوی آموزشی نصب اینجا⬇️  
[![ویدیوی آموزشی نصب](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### نسخه قابل حمل

فایل zip را از [صفحه انتشار GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) دانلود کرده و در هر پوشه‌ای باز کنید.  
- `WindowTranslator-(نسخه).zip` : نیاز به محیط .NET دارد  
- `WindowTranslator-full-(نسخه).zip` : مستقل از .NET

## نحوه استفاده

### Bergamot ![پیش‌فرض](https://img.shields.io/badge/پیش‌فرض-brightgreen)

1. `WindowTranslator.exe` را اجرا کنید و روی دکمه ترجمه کلیک کنید.  
   ![دکمه ترجمه](images/translate.png)
2. پنجره برنامه‌ای که می‌خواهید ترجمه شود را انتخاب کنید و روی «OK» کلیک کنید.  
   ![انتخاب پنجره](images/select.png)
3. از برگه «تنظیمات عمومی»، زبان‌های مبدأ و مقصد را در «تنظیمات زبان» انتخاب کنید.  
   ![تنظیمات زبان](images/language.png)
4. پس از تکمیل تنظیمات، روی «OK» کلیک کنید تا صفحه تنظیمات بسته شود.  
   > ممکن است نیاز به نصب قابلیت OCR باشد.
   > لطفاً دستورالعمل‌ها را برای نصب دنبال کنید.
5. پس از لحظه‌ای، نتایج ترجمه به عنوان پوشش نمایش داده می‌شود.  
   ![نتایج ترجمه](images/result.png)

> [!NOTE]
> چندین ماژول ترجمه در WindowTranslator موجود است.  
> ترجمه Google محدودیت پایینی بر مقدار متن قابل ترجمه دارد. اگر به طور مکرر استفاده می‌کنید، استفاده از ماژول‌های دیگر را در نظر بگیرید.  
> می‌توانید لیست ماژول‌های ترجمه موجود را در ویدیوهای زیر یا در [مستندات](https://wt.studiofreesia.com/TranslateModule.fa) بررسی کنید.
> 
> |                |                                                           ویدیوی استفاده                                                            | مزایا                    | معایب                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | کاملاً رایگان<br/>بدون محدودیت ترجمه<br/>ترجمه سریع | دقت ترجمه کمتر<br/>نیاز به بیش از 1 گیگابایت حافظه آزاد |
> |   ترجمه Google   | [![ویدیوی راه‌اندازی ترجمه Google](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | کاملاً رایگان | محدودیت ترجمه پایین<br/>دقت ترجمه کمتر |
> |     DeepL      |   [![ویدیوی راه‌اندازی DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | طبقه رایگان بزرگ<br/>ترجمه سریع | |
> |     Gemini     | [![ویدیوی راه‌اندازی Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | دقت ترجمه بالا | هزینه اندک لازم است |
> |    ChatGPT     | TBD | دقت ترجمه بالا | هزینه اندک لازم است |
> | LLM محلی | TBD | خدمت خود رایگان است | نیاز به رایانه با مشخصات بالا |

## ویژگی‌های دیگر

علاوه بر ماژول‌های ترجمه، WindowTranslator دارای ویژگی‌های متنوعی است.  
اگر می‌خواهید بیشتر بدانید، لطفاً به [Wiki](https://github.com/Freeesia/WindowTranslator/wiki) مراجعه کنید.

---
[سیاست حریم خصوصی](PrivacyPolicy.md)

این مستند از ژاپنی با استفاده از ترجمه ماشینی ترجمه شده است.
