# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator ist ein Tool zum Übersetzen von Windows-Anwendungsfenstern.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## Download

### Installationsversion ![Empfohlen](https://img.shields.io/badge/Empfohlen-brightgreen)
Laden Sie die MSI-Datei von der [GitHub-Releases-Seite](https://github.com/Freeesia/WindowTranslator/releases/latest) herunter und führen Sie sie aus, um WindowTranslator zu installieren.

[![Installationsversion](https://github.com/user-attachments/assets/b5babc02-715b-43bc-ba97-f23078ffd39b)](https://youtu.be/wvcbCLA9chQ?t=7)

### Portable Version
Laden Sie die ZIP-Datei von der [GitHub-Releases-Seite](https://github.com/Freeesia/WindowTranslator/releases/latest) herunter und entpacken Sie sie in einen beliebigen Ordner.

## Anwendung

### Video-Version
|                   | DeepL-Version | Google AI-Version |
| ----------------- | ------------- | ----------------- |
| Videolink         | [![DeepL-Einstellungs-Video](https://github.com/user-attachments/assets/4abd512f-cff9-45a8-852b-722641458f0b)](https://youtu.be/D7Yb6rIVPI0) | [![Google AI-Einstellungs-Video](https://github.com/user-attachments/assets/9d3a91ab-f1aa-4079-be68-622212ab1b68)](https://youtu.be/Oht0z03M91I) |
| Vorteile          | Schnelle Übersetzung, großzügiges kostenloses Kontingent | Höhere Übersetzungsgenauigkeit |
| Nachteile         | Geringere Übersetzungsgenauigkeit | Erfordert geringe Zahlung, langsamere Übersetzung |

### Voraussetzungen

#### DeepL API-Schlüssel erhalten
Registrieren Sie sich auf der [DeepL-Website](https://www.deepl.com/pro-api) und erhalten Sie Ihren API-Schlüssel.  
(Funktioniert mit kostenlosen und kostenpflichtigen Tarifen)

> DeepL wird als Übersetzungsmaschine verwendet.  
> Für die Nutzung generativer KI-Übersetzung konfigurieren Sie das [LLM Plugin](https://github.com/Freeesia/WindowTranslator/wiki/LLMPlugin).

## Start

#### Ersteinrichtung

1. Starten Sie `WindowTranslator.exe` und öffnen Sie den Einstellungsbildschirm.  
   ![Einstellungen](images/settings.png)
2. Wählen Sie im Tab "Allgemeine Einstellungen" unter "Spracheinstellungen" die Quell- und Zielsprache aus.  
   ![Spracheinstellungen](images/language.png)
3. Wählen Sie im Tab "Plugin-Einstellungen" unter "Übersetzungsmodul" den Punkt "DeepL".  
   ![Translation Module](images/translate_module.png)
4. Geben Sie im Tab "DeepLOptions" Ihren DeepL API-Schlüssel ein.  
   ![DeepL-Einstellungen](images/deepl.png)
5. Klicken Sie auf "OK", um den Einstellungsbildschirm zu schließen.

> Beim Schließen müssen Sie möglicherweise OCR-Funktionalitäten installieren, um die Quellsprache zu erkennen. Bitte folgen Sie den Anweisungen.

#### Übersetzungsstart

1. Starten Sie `WindowTranslator.exe` und klicken Sie auf die Schaltfläche "Übersetzen".  
   ![Übersetzen-Schaltfläche](images/translate.png)
2. Wählen Sie das Fenster der Anwendung, die übersetzt werden soll, und klicken Sie auf "OK".  
   ![Fensterauswahl](images/select.png)
3. Das Übersetzungsergebnis erscheint als Overlay.  
   ![Übersetzungsergebnis](images/result.png)

## Weitere Funktionen

Weitere Informationen finden Sie im [Wiki](https://github.com/Freeesia/WindowTranslator/wiki).

---  
Datenschutzerklärung: [Datenschutzerklärung](PrivacyPolicy.de.md)

> ※ Dieses Dokument wurde maschinell übersetzt.