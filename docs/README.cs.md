# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator je nástroj pro překlad oken aplikací v systému Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md) | [PT-BR](./README.pt-BR.md) | [FR](./README.fr.md) | [ES](./README.es.md) | [AR](./README.ar.md) | [TR](./README.tr.md) | [TH](./README.th.md) | [RU](./README.ru.md) | [FIL](./README.fil.md) | [PL](./README.pl.md) | [FA](./README.fa.md) | [CS](./README.cs.md)

## Obsah
- [ WindowTranslator](#-windowtranslator)
  - [Obsah](#obsah)
  - [Stažení](#stažení)
    - [Verze Microsoft Store ](#verze-microsoft-store-)
    - [Instalační verze](#instalační-verze)
    - [Přenosná verze](#přenosná-verze)
  - [Jak používat](#jak-používat)
    - [Bergamot ](#bergamot-)
  - [Další funkce](#další-funkce)

## Stažení
### Verze Microsoft Store ![Doporučeno](https://img.shields.io/badge/Doporučeno-brightgreen)

Nainstalujte z [Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct).
Funguje i v prostředích, kde není nainstalováno .NET.

### Instalační verze

Stáhněte `WindowTranslator-(verze).msi` ze [stránky vydání GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) a spusťte jej pro instalaci.  
Výukové video pro instalaci je zde⬇️  
[![Video s návodem k instalaci](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Přenosná verze

Stáhněte soubor zip ze [stránky vydání GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) a rozbalte jej do libovolné složky.  
- `WindowTranslator-(verze).zip` : Vyžaduje prostředí .NET  
- `WindowTranslator-full-(verze).zip` : Nezávislé na .NET

## Jak používat

### Bergamot ![Výchozí](https://img.shields.io/badge/Výchozí-brightgreen)

1. Spusťte `WindowTranslator.exe` a klikněte na tlačítko překladu.  
   ![Tlačítko překladu](images/translate.png)
2. Vyberte okno aplikace, kterou chcete přeložit, a klikněte na tlačítko „OK".  
   ![Výběr okna](images/select.png)
3. Na kartě „Obecná nastavení" vyberte zdrojový a cílový jazyk v části „Nastavení jazyka".  
   ![Nastavení jazyka](images/language.png)
4. Po dokončení nastavení klikněte na tlačítko „OK" pro zavření obrazovky nastavení.  
   > Může být vyžadována instalace funkce OCR.
   > Postupujte podle pokynů k instalaci.
5. Po chvíli se výsledky překladu zobrazí jako překryv.  
   ![Výsledky překladu](images/result.png)

> [!NOTE]
> V aplikaci WindowTranslator jsou k dispozici různé překladové moduly.  
> Google Translate má nízký limit množství textu, který lze přeložit. Pokud jej používáte často, zvažte použití jiných modulů.  
> Seznam dostupných překladových modulů najdete ve videích níže nebo v [Dokumentaci](https://wt.studiofreesia.com/TranslateModule.en).
> 
> |                |                                                           Výukové video                                                            | Výhody                    | Nevýhody                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | Zcela zdarma<br/>Bez omezení překladu<br/>Rychlý překlad | Nižší přesnost překladu<br/>Vyžaduje více než 1 GB volné paměti |
> |   Google Translate   | [![Video nastavení Google Translate](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Zcela zdarma | Nízký limit překladu<br/>Nižší přesnost překladu |
> |     DeepL      |   [![Video nastavení DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Velká bezplatná kvóta<br/>Rychlý překlad | |
> |     Gemini     | [![Video nastavení Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Vysoká přesnost překladu | Vyžaduje malý poplatek |
> |    ChatGPT     | TBD | Vysoká přesnost překladu | Vyžaduje malý poplatek |
> | Lokální LLM | TBD | Samotná služba je zdarma | Vyžaduje výkonný počítač |

## Další funkce

Kromě překladových modulů má WindowTranslator různé funkce.  
Pokud se chcete dozvědět více, podívejte se na [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[Zásady ochrany osobních údajů](PrivacyPolicy.cs.md)

Tento dokument byl přeložen z japonštiny pomocí strojového překladu.
