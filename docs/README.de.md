# <img src="images/wt.png" width="32" > WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Freeesia/WindowTranslator)](https://github.com/Freeesia/WindowTranslator/releases/latest)
[![NuGet Version](https://img.shields.io/nuget/v/WindowTranslator.Abstractions)](https://www.nuget.org/packages/WindowTranslator.Abstractions)
[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Freeesia/WindowTranslator/total)](https://github.com/Freeesia/WindowTranslator/releases/latest)

WindowTranslator ist ein Tool zum Übersetzen von Windows-Anwendungsfenstern.

[JA](README.md) | [EN](./README.en.md) | [DE](./README.de.md) | [KR](./README.kr.md) | [ZH-CN](./README.zh-cn.md) | [ZH-TW](./README.zh-tw.md)

## Download

Laden Sie die ZIP-Datei von der [GitHub-Releases-Seite](https://github.com/Freeesia/WindowTranslator/releases/latest) herunter und entpacken Sie sie in einem beliebigen Ordner.

* `WindowTranslator-(Version).zip` funktioniert in Umgebungen mit .NET installiert
* `WindowTranslator-full-(Version).zip` funktioniert auch in Umgebungen ohne .NET installiert

## Verwendung

### Voraussetzungen

#### DeepL API-Schlüssel erhalten

Registrieren Sie sich als Benutzer auf der [DeepL-Website](https://www.deepl.com/pro-api) und erhalten Sie einen API-Schlüssel.   
(Der API-Schlüssel für den kostenlosen Tarif wurde getestet, aber es wird erwartet, dass er auch mit API-Schlüsseln für kostenpflichtige Tarife funktioniert)

### Starten

#### Ersteinrichtung

1. Starten Sie `WindowTranslator.exe` und öffnen Sie den Einstellungsbildschirm.  
  ![Einstellungen](images/settings.png)
2. Wählen Sie die Quell- und Zielsprachen unter "Spracheinstellungen" auf der Registerkarte "Allgemeine Einstellungen" aus.   
  ![Spracheinstellungen](images/language.png)
3. Geben Sie Ihren DeepL-API-Schlüssel im Feld "API Key" auf der Registerkarte "DeepLOptions" ein.  
  ![DeepL-Einstellungen](images/deepl.png)
4. Nach Abschluss der Einstellungen klicken Sie auf die Schaltfläche "OK", um den Einstellungsbildschirm zu schließen.

#### Übersetzung starten

1. Starten Sie `WindowTranslator.exe` und klicken Sie auf die Schaltfläche "Übersetzen".  
  ![Übersetzen Schaltfläche](images/translate.png)
2. Wählen Sie das Fenster der Anwendung aus, die Sie übersetzen möchten, und klicken Sie auf die Schaltfläche "OK".   
  ![Fensterauswahl](images/select.png)
3. Das Übersetzungsergebnis wird als Overlay angezeigt.   
  ![Übersetzungsergebnis](images/result.png)

> Translated with ChatGPT