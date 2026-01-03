# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator to narzędzie do tłumaczenia okien aplikacji w systemie Windows.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md) | [HI](./README.hi.md) | [MS](./README.ms.md) | [ID](./README.id.md) | [PT-BR](./README.pt-BR.md) | [FR](./README.fr.md) | [ES](./README.es.md) | [AR](./README.ar.md) | [TR](./README.tr.md) | [TH](./README.th.md) | [RU](./README.ru.md) | [FIL](./README.fil.md) | [PL](./README.pl.md)

## Spis treści
- [ WindowTranslator](#-windowtranslator)
  - [Spis treści](#spis-treści)
  - [Pobierz](#pobierz)
    - [Wersja Microsoft Store ](#wersja-microsoft-store-)
    - [Wersja instalacyjna](#wersja-instalacyjna)
    - [Wersja przenośna](#wersja-przenośna)
  - [Jak używać](#jak-używać)
    - [Bergamot ](#bergamot-)
  - [Inne funkcje](#inne-funkcje)

## Pobierz
### Wersja Microsoft Store ![Zalecane](https://img.shields.io/badge/Zalecane-brightgreen)

Zainstaluj ze sklepu [Microsoft Store](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct).
Działa nawet w środowiskach, gdzie .NET nie jest zainstalowany.

### Wersja instalacyjna

Pobierz `WindowTranslator-(wersja).msi` ze strony [wydań GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) i uruchom, aby zainstalować.  
Film instruktażowy dotyczący instalacji znajduje się tutaj⬇️  
[![Film instruktażowy instalacji](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Wersja przenośna

Pobierz plik zip ze strony [wydań GitHub](https://github.com/Freeesia/WindowTranslator/releases/latest) i rozpakuj go do dowolnego folderu.  
- `WindowTranslator-(wersja).zip` : Wymaga środowiska .NET  
- `WindowTranslator-full-(wersja).zip` : Niezależny od .NET

## Jak używać

### Bergamot ![Domyślny](https://img.shields.io/badge/Domyślny-brightgreen)

1. Uruchom `WindowTranslator.exe` i kliknij przycisk tłumaczenia.  
   ![Przycisk tłumaczenia](images/translate.png)
2. Wybierz okno aplikacji, które chcesz przetłumaczyć, i kliknij przycisk „OK".  
   ![Wybór okna](images/select.png)
3. W zakładce „Ustawienia ogólne" wybierz język źródłowy i docelowy w „Ustawieniach języka".  
   ![Ustawienia języka](images/language.png)
4. Po zakończeniu ustawień kliknij przycisk „OK", aby zamknąć ekran ustawień.  
   > Może być wymagana instalacja funkcji OCR.
   > Postępuj zgodnie z instrukcjami, aby zainstalować.
5. Po chwili wyniki tłumaczenia zostaną wyświetlone jako nakładka.  
   ![Wyniki tłumaczenia](images/result.png)

> [!NOTE]
> W WindowTranslator dostępnych jest wiele modułów tłumaczenia.  
> Google Translate ma niski limit ilości tekstu, który można przetłumaczyć. Jeśli używasz go często, rozważ użycie innych modułów.  
> Listę dostępnych modułów tłumaczenia możesz sprawdzić w filmach poniżej lub w [Dokumentacji](https://wt.studiofreesia.com/TranslateModule.pl).
> 
> |                |                                                           Film instruktażowy                                                            | Zalety                    | Wady                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | Całkowicie darmowy<br/>Brak limitu tłumaczeń<br/>Szybkie tłumaczenie | Niższa dokładność tłumaczeń<br/>Wymaga ponad 1GB wolnej pamięci |
> |   Google Translate   | [![Film konfiguracji Google Translate](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Całkowicie darmowy | Niski limit tłumaczeń<br/>Niższa dokładność tłumaczeń |
> |     DeepL      |   [![Film konfiguracji DeepL](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Duży darmowy plan<br/>Szybkie tłumaczenie | |
> |     Gemini     | [![Film konfiguracji Google AI](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Wysoka dokładność tłumaczeń | Niewielka opłata wymagana |
> |    ChatGPT     | Do ustalenia | Wysoka dokładność tłumaczeń | Niewielka opłata wymagana |
> | Lokalny LLM | Do ustalenia | Sama usługa jest darmowa | Wymaga wysokiej specyfikacji PC |

## Inne funkcje

Oprócz modułów tłumaczenia, WindowTranslator ma różne funkcje.  
Jeśli chcesz dowiedzieć się więcej, sprawdź [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[Polityka prywatności](PrivacyPolicy.pl.md)

Ten dokument został przetłumaczony z japońskiego przy użyciu tłumaczenia maszynowego.
