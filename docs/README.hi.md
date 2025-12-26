# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator Windows पर एप्लिकेशन की विंडो का अनुवाद करने के लिए एक उपकरण है।

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md) | [PT-BR](./README.pt-BR.md) | [FR](./README.fr.md) | [ES](./README.es.md) | [AR](./README.ar.md) | [TR](./README.tr.md) | [TH](./README.th.md) | [RU](./README.ru.md) | [FIL](./README.fil.md)

## विषय सूची
- [ WindowTranslator](#-windowtranslator)
  - [विषय सूची](#विषय-सूची)
  - [डाउनलोड](#डाउनलोड)
    - [Microsoft Store संस्करण ](#microsoft-store-संस्करण-)
    - [इंस्टॉलर संस्करण](#इंस्टॉलर-संस्करण)
    - [पोर्टेबल संस्करण](#पोर्टेबल-संस्करण)
  - [उपयोग कैसे करें](#उपयोग-कैसे-करें)
    - [Bergamot ](#bergamot-)
  - [अन्य सुविधाएँ](#अन्य-सुविधाएँ)

## डाउनलोड
### Microsoft Store संस्करण ![अनुशंसित](https://img.shields.io/badge/अनुशंसित-brightgreen)

[Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct) से इंस्टॉल करें।
.NET इंस्टॉल नहीं किए गए वातावरण में भी काम करता है।

### इंस्टॉलर संस्करण

[GitHub रिलीज़ पेज](https://github.com/Freeesia/WindowTranslator/releases/latest) से `WindowTranslator-(version).msi` डाउनलोड करें और इंस्टॉल करने के लिए इसे चलाएं।  
इंस्टॉलेशन गाइड वीडियो यहाँ है⬇️  
[![इंस्टॉलेशन गाइड वीडियो](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### पोर्टेबल संस्करण

[GitHub रिलीज़ पेज](https://github.com/Freeesia/WindowTranslator/releases/latest) से zip फ़ाइल डाउनलोड करें और इसे किसी भी फ़ोल्डर में एक्सट्रैक्ट करें।  
- `WindowTranslator-(version).zip` : .NET वातावरण की आवश्यकता है  
- `WindowTranslator-full-(version).zip` : .NET स्वतंत्र

## उपयोग कैसे करें

### Bergamot ![डिफ़ॉल्ट](https://img.shields.io/badge/डिफ़ॉल्ट-brightgreen)

1. `WindowTranslator.exe` लॉन्च करें और अनुवाद बटन पर क्लिक करें।  
   ![अनुवाद बटन](images/translate.png)
2. जिस एप्लिकेशन का आप अनुवाद करना चाहते हैं उसकी विंडो चुनें और "OK" बटन पर क्लिक करें।  
   ![विंडो चयन](images/select.png)
3. "संपूर्ण सेटिंग्स" टैब के "भाषा सेटिंग्स" से अनुवाद स्रोत भाषा और अनुवाद लक्ष्य भाषा चुनें।  
   ![भाषा सेटिंग्स](images/language.png)
4. सेटिंग्स पूर्ण करने के बाद, सेटिंग्स स्क्रीन बंद करने के लिए "OK" बटन पर क्लिक करें।  
   > OCR फ़ंक्शन इंस्टॉलेशन की आवश्यकता हो सकती है।
   > कृपया इंस्टॉल करने के लिए निर्देशों का पालन करें।
5. कुछ समय बाद, अनुवाद परिणाम ओवरले के रूप में प्रदर्शित होंगे।  
   ![अनुवाद परिणाम](images/result.png)

> [!NOTE]
> WindowTranslator में विभिन्न अनुवाद मॉड्यूल उपलब्ध हैं।  
> Google अनुवाद में अनुवाद किए जा सकने वाले टेक्स्ट की मात्रा की सीमा कम है। यदि आप इसे बार-बार उपयोग करते हैं, तो अन्य मॉड्यूल का उपयोग करने पर विचार करें।  
> आप नीचे दिए गए वीडियो में या [दस्तावेज़](https://wt.studiofreesia.com/TranslateModule.hi) पर उपलब्ध अनुवाद मॉड्यूल की सूची देख सकते हैं।
> 
> |                |                                                           उपयोग वीडियो                                                            | लाभ                    | नुकसान                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | पूरी तरह से मुफ्त<br/>कोई अनुवाद सीमा नहीं<br/>तेज अनुवाद | कम अनुवाद सटीकता<br/>1GB से अधिक मुक्त मेमोरी की आवश्यकता |
> |   Google अनुवाद   | [![Google अनुवाद सेटअप वीडियो](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | पूरी तरह से मुफ्त | कम अनुवाद सीमा<br/>कम अनुवाद सटीकता |
> |     DeepL      |   [![DeepL सेटअप वीडियो](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | बड़ा मुफ्त टियर<br/>तेज अनुवाद | |
> |     Gemini     | [![Google AI सेटअप वीडियो](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | उच्च अनुवाद सटीकता | छोटा शुल्क आवश्यक |
> |    ChatGPT     | TBD | उच्च अनुवाद सटीकता | छोटा शुल्क आवश्यक |
> | स्थानीय LLM | TBD | सेवा स्वयं मुफ्त है | उच्च-विशिष्टता PC आवश्यक |

## अन्य सुविधाएँ

अनुवाद मॉड्यूल के अलावा, WindowTranslator में विभिन्न सुविधाएँ भी लगाई गई हैं।  
यदि आप और अधिक जानना चाहते हैं, तो कृपया [Wiki](https://github.com/Freeesia/WindowTranslator/wiki) देखें।

---
[गोपनीयता नीति](PrivacyPolicy.md)

यह दस्तावेज़ मशीन अनुवाद का उपयोग करके जापानी से अनुवादित किया गया था।
