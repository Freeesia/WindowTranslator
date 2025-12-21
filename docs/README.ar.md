# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/ar-sa%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator هو أداة لترجمة نوافذ التطبيقات على Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md) | [PT-BR](./README.pt-BR.md) | [FR](./README.fr.md) | [ES](./README.es.md) | [AR](./README.ar.md) | [TR](./README.tr.md) | [TH](./README.th.md) | [RU](./README.ru.md) | [FIL](./README.fil.md)

## جدول المحتويات
- [ WindowTranslator](#-windowtranslator)
  - [جدول المحتويات](#جدول-المحتويات)
  - [تحميل](#تحميل)
    - [نسخة Microsoft Store ](#نسخة-microsoft-store-)
    - [نسخة المثبت](#نسخة-المثبت)
    - [نسخة محمولة](#نسخة-محمولة)
  - [كيفية الاستخدام](#كيفية-الاستخدام)
    - [Bergamot ](#bergamot-)
  - [ميزات أخرى](#ميزات-أخرى)

## تحميل
### نسخة Microsoft Store ![موصى بها](https://img.shields.io/badge/موصى%20بها-brightgreen)

قم بالتثبيت من [Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct).
يعمل حتى في البيئات التي لم يتم تثبيت .NET فيها.

### نسخة المثبت

قم بتنزيل `WindowTranslator-(الإصدار).msi` من [صفحة إصدارات GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) وقم بتشغيله للتثبيت.  
فيديو تعليمي للتثبيت هنا⬇️  
[![فيديو تعليمي للتثبيت](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### نسخة محمولة

قم بتنزيل ملف zip من [صفحة إصدارات GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) واستخرجه إلى أي مجلد.  
- `WindowTranslator-(الإصدار).zip` : يتطلب بيئة .NET  
- `WindowTranslator-full-(الإصدار).zip` : مستقل عن .NET

## كيفية الاستخدام

### Bergamot ![افتراضي](https://img.shields.io/badge/افتراضي-brightgreen)

1. قم بتشغيل `WindowTranslator.exe` وانقر على زر الترجمة.  
   ![زر الترجمة](images/translate.png)
2. حدد نافذة التطبيق الذي تريد ترجمته وانقر على زر "OK".  
   ![اختيار النافذة](images/select.png)
3. من علامة التبويب "الإعدادات العامة"، حدد لغات المصدر والهدف في "إعدادات اللغة".  
   ![إعدادات اللغة](images/language.png)
4. بعد إكمال الإعدادات، انقر على زر "OK" لإغلاق شاشة الإعدادات.  
   > قد تكون هناك حاجة لتثبيت وظيفة OCR.
   > يرجى اتباع التعليمات للتثبيت.
5. بعد لحظة، ستظهر نتائج الترجمة كطبقة علوية.  
   ![نتائج الترجمة](images/result.png)

> [!NOTE]
> تتوفر وحدات ترجمة متعددة في WindowTranslator.  
> ترجمة Google لديها حد منخفض على كمية النص التي يمكن ترجمتها. إذا كنت تستخدمها بشكل متكرر، فكر في استخدام وحدات أخرى.  
> يمكنك التحقق من قائمة وحدات الترجمة المتاحة في مقاطع الفيديو أدناه أو في [الوثائق](https://wt.studiofreesia.com/TranslateModule.ar).
> 
> |                |                                                           فيديو الاستخدام                                                            | المزايا                    | العيوب                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | مجاني تماماً<br/>بدون حد للترجمة<br/>ترجمة سريعة | دقة ترجمة أقل<br/>يتطلب أكثر من 1 جيجابايت من الذاكرة الحرة |
> |   ترجمة Google   | [![فيديو إعداد ترجمة Google](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | مجاني تماماً | حد ترجمة منخفض<br/>دقة ترجمة أقل |
> |     DeepL      |   [![فيديو إعداد DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | طبقة مجانية كبيرة<br/>ترجمة سريعة | |
> |     Gemini     | [![فيديو إعداد Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | دقة ترجمة عالية | رسوم صغيرة مطلوبة |
> |    ChatGPT     | TBD | دقة ترجمة عالية | رسوم صغيرة مطلوبة |
> | LLM محلي | TBD | الخدمة نفسها مجانية | يتطلب كمبيوتر عالي المواصفات |

## ميزات أخرى

بالإضافة إلى وحدات الترجمة، يحتوي WindowTranslator على ميزات متنوعة.  
إذا كنت تريد معرفة المزيد، يرجى مراجعة [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[سياسة الخصوصية](PrivacyPolicy.md)

تمت ترجمة هذا المستند من اليابانية باستخدام الترجمة الآلية.
