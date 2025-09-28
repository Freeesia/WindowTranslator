# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![Crowdin](https://badges.crowdin.net/windowtranslator/localized.svg)](https://crowdin.com/project/windowtranslator)
[![Microsoft Store](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9pjd2fdzqxm3?referrer=appbadge&mode=direct)

WindowTranslator ist ein Tool zum Übersetzen von Windows-Anwendungsfenstern.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md) | [VI](./README.vi.md)

## Inhaltsverzeichnis
- [ WindowTranslator](#-windowtranslator)
  - [Inhaltsverzeichnis](#inhaltsverzeichnis)
  - [Download](#download)
    - [Installationsversion ](#installationsversion-)
    - [Portable Version](#portable-version)
  - [Verwendung](#verwendung)
    - [Bergamot ](#bergamot-)
  - [Weitere Funktionen](#weitere-funktionen)

## Download
### Installationsversion ![Empfohlen](https://img.shields.io/badge/Empfohlen-brightgreen)

Laden Sie `WindowTranslator-(Version).msi` von der [GitHub-Release-Seite](https://github.com/Freeesia/WindowTranslator/releases/latest) herunter und führen Sie es zur Installation aus.  
Installationsanleitung Video⬇️  
[![Installationsanleitung Video](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Portable Version

Laden Sie die Zip-Datei von der [GitHub-Release-Seite](https://github.com/Freeesia/WindowTranslator/releases/latest) herunter und extrahieren Sie sie in einen beliebigen Ordner.  
- `WindowTranslator-(Version).zip` : Erfordert .NET-Umgebung  
- `WindowTranslator-full-(Version).zip` : .NET-unabhängig

## Verwendung

### Bergamot ![Standard](https://img.shields.io/badge/Standard-brightgreen)

1. Starten Sie `WindowTranslator.exe` und klicken Sie auf die Übersetzen-Schaltfläche.  
   ![Übersetzen-Schaltfläche](images/translate.png)
2. Wählen Sie das Fenster der Anwendung aus, die Sie übersetzen möchten, und klicken Sie auf die Schaltfläche "OK".  
   ![Fensterauswahl](images/select.png)
3. Wählen Sie auf der Registerkarte "Allgemeine Einstellungen" die Quell- und Zielsprache in den "Spracheinstellungen" aus.  
   ![Spracheinstellungen](images/language.png)
4. Klicken Sie nach Abschluss der Einstellungen auf die Schaltfläche "OK", um den Einstellungsbildschirm zu schließen.  
   > Die Installation der OCR-Funktion kann erforderlich sein.
   > Bitte folgen Sie den Anweisungen zur Installation.
5. Nach einer Weile werden die Übersetzungsergebnisse als Overlay angezeigt.  
   ![Übersetzungsergebnis](images/result.png)

> [!NOTE]
> In WindowTranslator stehen verschiedene Übersetzungsmodule zur Verfügung.  
> Google Translate hat eine niedrige Grenze für die Menge an Text, die übersetzt werden kann. Wenn Sie es häufig verwenden, sollten Sie die Verwendung anderer Module in Betracht ziehen.  
> Die Liste der verfügbaren Übersetzungsmodule finden Sie in den Videos unten oder im [Wiki](https://github.com/Freeesia/WindowTranslator/wiki#翻訳).
> 
> |                |                                                         Anleitungsvideo                                                          | Vorteile                    | Nachteile                        |
> | :------------: | :-----------------------------------------------------------------------------------------------------------------------------------: | :---------------------------- | :----------------------------------- |
> |   Bergamot     | | Völlig kostenlos<br/>Keine Übersetzungsbegrenzung<br/>Schnelle Übersetzung | Geringere Übersetzungsgenauigkeit<br/>Erfordert mehr als 1 GB freien Speicher |
> |   Google Translate   | [![Google Translate Einrichtungsvideo](https://github.com/user-attachments/assets/bbf45370-0387-47e1-b690-3183f37e06d2)](https://youtu.be/83A8T890N5M)  | Völlig kostenlos | Niedrige Übersetzungsbegrenzung<br/>Geringere Übersetzungsgenauigkeit |
> |     DeepL      |   [![DeepL Einrichtungsvideo](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0)   | Großes kostenloses Kontingent<br/>Schnelle Übersetzung | |
> |     Gemini     | [![Google AI Einrichtungsvideo](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Hohe Übersetzungsgenauigkeit | Kleine Gebühr erforderlich |
> |    ChatGPT     | TBD | Hohe Übersetzungsgenauigkeit | Kleine Gebühr erforderlich |
> | Lokales LLM | TBD | Service selbst ist kostenlos | Leistungsstarker PC erforderlich |

## Weitere Funktionen

Neben Übersetzungsmodulen verfügt WindowTranslator über verschiedene Funktionen.  
Wenn Sie mehr erfahren möchten, besuchen Sie bitte das [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---
[Datenschutzrichtlinie](PrivacyPolicy.md)

Dieses Dokument wurde mit maschineller Übersetzung aus dem Japanischen übersetzt.