# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator ist ein Tool, um die Fenster von Windows-Anwendungen zu übersetzen.

[JA](README.md) | [EN](README.en.md) | [DE](README.de.md) | [KR](README.kr.md) | [ZH-CN](README.zh-cn.md) | [ZH-TW](README.zh-tw.md)

## Inhaltsverzeichnis
- [ WindowTranslator](#-windowtranslator)
  - [Inhaltsverzeichnis](#inhaltsverzeichnis)
  - [Download](#download)
    - [Installer-Version ](#installer-version-)
    - [Portable Version](#portable-version)
  - [Bedienung](#bedienung)
    - [Google Übersetzer ](#google-übersetzer-)
  - [Weitere Funktionen](#weitere-funktionen)

## Download
### Installer-Version ![Empfohlen](https://img.shields.io/badge/%E3%82%AA%E3%82%B9%E3%82%B9%E3%83%A1-brightgreen)
Laden Sie die `WindowTranslator-(Version).msi` von der [GitHub Releases Seite](https://github.com/Freeesia/WindowTranslator/releases/latest) herunter und führen Sie sie aus, um zu installieren.  
Installationsvideo hier:  
[![Installationsvideo](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Portable Version
Laden Sie die Zip-Datei von der [GitHub Releases Seite](https://github.com/Freeesia/WindowTranslator/releases/latest) herunter und entpacken Sie sie in einen Ordner.  
- `WindowTranslator-(Version).zip` benötigt eine .NET-Umgebung.  
- `WindowTranslator-full-(Version).zip` benötigt kein .NET.

## Bedienung

### Google Übersetzer ![Standard](https://img.shields.io/badge/Default-brightgreen)

1. Starten Sie `WindowTranslator.exe` und klicken Sie auf den Übersetzen-Button.  
   ![Übersetzen-Button](images/translate.png)
2. Wählen Sie das Fenster der Anwendung aus, die übersetzt werden soll, und klicken Sie auf "OK".  
   ![Fensterauswahl](images/select.png)
3. Wählen Sie im Tab "Allgemeine Einstellungen" unter "Spracheinstellungen" die Quell- und Zielsprache.  
   ![Spracheinstellungen](images/language.png)
4. Bestätigen Sie die Einstellungen mit "OK" und schließen Sie den Einstellungsdialog.  
   > Falls OCR erforderlich ist, folgen Sie bitte den Anweisungen zur Installation.
5. Der Browser öffnet sich und zeigt den Google-Login-Bereich an.  
   ![Login-Bildschirm](images/login.png)
6. Nach dem Login wählen Sie "Alle auswählen" für die Berechtigungen und klicken auf "Weiter".  
   ![Autorisierungsbildschirm](images/auth.png)
7. Nach kurzer Zeit wird das Übersetzungsergebnis als Overlay angezeigt.  
   ![Übersetzungsergebnis](images/result.png)

> [!NOTE]
> WindowTranslator unterstützt verschiedene Übersetzungsmodule. Hier wird die Standardmethode mit Google Übersetzer gezeigt.  
> Google Übersetzer hat ein geringeres Übersetzungsvolumen. Bei hoher Nutzung sollten Sie andere Module in Betracht ziehen.  
> Eine vollständige Liste der verfügbaren Übersetzungsmodule finden Sie im Video oder im [Wiki](https://github.com/Freeesia/WindowTranslator/wiki#translation).
> 
> |                |                                Anleitungsvideo                                | Vorteile                      | Nachteile                             |
> | :------------: | :-------------------------------------------------------------------------: | :---------------------------- | :------------------------------------ |
> | Google Übersetzer |                                  TBD                                     | Einfache Einrichtung<br/>Kostenlos | Geringes Übersetzungsvolumen<br/>Niedrigere Genauigkeit |
> | DeepL         | [![DeepL Setup Video](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0) | Großzügige Gratisstufe<br/>Schnelle Übersetzung | Geringere Genauigkeit                |
> | GoogleAI      | [![Google AI Setup Video](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) | Hohe Genauigkeit              | Kleine Gebühren erforderlich          |
> | LLM (Cloud)   |                                  TBD                                     | Hohe Genauigkeit              | Kleine Gebühren erforderlich          |
> | LLM (Local)   |                                  TBD                                     | Kostenloser Service           | Leistungsstarker PC erforderlich       |

## Weitere Funktionen

Weitere Funktionen finden Sie im [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---  
Datenschutzerklärung: [Datenschutzerklärung](PrivacyPolicy.de.md)

> ※ Dieses Dokument wurde maschinell übersetzt.