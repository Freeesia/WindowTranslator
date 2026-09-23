# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator egy Windows-alkalmazás, amely lefordítja az alkalmazások ablakszövegét.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md) | [PT-BR](./README.pt-BR.md) | [FR](./README.fr.md) | [ES](./README.es.md) | [AR](./README.ar.md) | [TR](./README.tr.md) | [TH](./README.th.md) | [RU](./README.ru.md) | [FIL](./README.fil.md) | [PL](./README.pl.md) | [FA](./README.fa.md) | [CS](./README.cs.md) | [HU](./README.hu.md)

## Tartalomjegyzék
- [ WindowTranslator](#-windowtranslator)
  - [Tartalomjegyzék](#tartalomjegyzék)
  - [Letöltés](#letöltés)
    - [Microsoft Store verzió ](#microsoft-store-verzió-)
    - [Telepítési verzió](#telepítési-verzió)
    - [Hordozható verzió](#hordozható-verzió)
  - [Használat](#használat)
    - [Bergamot ](#bergamot-)
  - [Egyéb funkciók](#egyéb-funkciók)

## Letöltés
### Microsoft Store verzió ![Ajánlott](https://img.shields.io/badge/Ajánlott-brightgreen)

Telepítse a [Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)-ból.
.NET nélküli környezetben is működik.

### Telepítési verzió

Töltse le a `WindowTranslator-(verzió).msi` fájlt a [GitHub kiadási oldalról](https://github.com/Freeesia/WindowTranslator/releases/latest), és futtassa a telepítéshez.  
Telepítési útmutató videó itt⬇️  
[![Telepítési útmutató videó](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Hordozható verzió

Töltse le a zip fájlt a [GitHub kiadási oldalról](https://github.com/Freeesia/WindowTranslator/releases/latest), és csomagolja ki egy tetszőleges mappába.  
- `WindowTranslator-(verzió).zip` : .NET környezet szükséges  
- `WindowTranslator-full-(verzió).zip` : .NET független

## Használat

### Bergamot ![Alapértelmezett](https://img.shields.io/badge/Alapértelmezett-brightgreen)

1. Futtassa a `WindowTranslator.exe` fájlt, és kattintson a fordítás gombra.  
   ![Fordítás gomb](images/translate.png)
2. Válassza ki a lefordítani kívánt alkalmazás ablakát, és kattintson az „OK" gombra.  
   ![Ablak kiválasztása](images/select.png)
3. Az „Általános beállítások" lapon válassza ki a forrás- és célnyelvet a „Nyelvi beállítások" részben.  
   ![Nyelvi beállítások](images/language.png)
4. A beállítások elvégzése után kattintson az „OK" gombra a beállítási képernyő bezárásához.  
   > Az OCR telepítése szükséges lehet.
   > Kövesse a telepítési utasításokat.
5. Rövid idő múlva a fordítási eredmények átfedő rétegként jelennek meg.  
   ![Fordítási eredmények](images/result.png)

> [!NOTE]
> A WindowTranslatorban különböző fordítási modulok érhetők el.  
> A Google Translate alacsony limitet szab a lefordítható szöveg mennyiségére. Ha gyakran használja, fontolja meg más modulok alkalmazását.  
> Az elérhető fordítási modulok listáját az alábbi videókban vagy a [Dokumentációban](https://wt.studiofreesia.com/TranslateModule.en) találja.
> 
> |                |                                                           Útmutató videó                                                            | Előnyök                    | Hátrányok                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | Teljesen ingyenes<br/>Korlátlan fordítás<br/>Gyors fordítás | Alacsonyabb fordítási pontosság<br/>Több mint 1 GB szabad memória szükséges |
> |   Google Translate   | [![Google Translate beállítási videó](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Teljesen ingyenes | Alacsony fordítási limit<br/>Alacsonyabb fordítási pontosság |
> |     DeepL      |   [![DeepL beállítási videó](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Nagy ingyenes kvóta<br/>Gyors fordítás | |
> |     Gemini     | [![Google AI beállítási videó](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Magas fordítási pontosság | Kis díjat igényel |
> |    ChatGPT     | TBD | Magas fordítási pontosság | Kis díjat igényel |
> | Helyi LLM | TBD | Maga a szolgáltatás ingyenes | Nagy teljesítményű számítógép szükséges |

## Egyéb funkciók

A fordítási modulokon kívül a WindowTranslatornak különböző funkciói vannak.  
Ha többet szeretne megtudni, látogasson el a [Wiki](https://github.com/Freeesia/WindowTranslator/wiki) oldalra.

---
[Adatvédelmi irányelvek](PrivacyPolicy.hu.md)

Ezt a dokumentumot gépi fordítással fordítottuk japánból.
