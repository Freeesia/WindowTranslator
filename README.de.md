# WindowTranslator

[![App Build](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/Freeesia/WindowTranslator/actions/workflows/dotnet-desktop.yml)
[![GitHub version](https://badge.fury.io/gh/Freeesia%2FWindowTranslator.svg)](https://badge.fury.io/gh/Freeesia%2FWindowTranslator)
[![NuGet version](https://badge.fury.io/nu/WindowTranslator.Abstractions.svg)](https://badge.fury.io/nu/WindowTranslator.Abstractions)

WindowTranslator ist ein Tool zum Übersetzen von Windows-Anwendungsfenstern.

## Download

Laden Sie die ZIP-Datei von der [GitHub-Releases-Seite](https://github.com/Freeesia/WindowTranslator/releases/latest) herunter und entpacken Sie sie in einem beliebigen Ordner.

* `WindowTranslator-(Version).zip` funktioniert in Umgebungen mit .NET installiert
* `WindowTranslator-full-(Version).zip` funktioniert auch in Umgebungen ohne .NET installiert

## Verwendung

### Voraussetzungen

#### Spracheinstellungen

Fügen Sie die Quell- und Zielsprachen für die Übersetzung in Ihre Windows-Spracheinstellungen hinzu.
[So fügen Sie Sprachen zu Windows hinzu](https://support.microsoft.com/de-de/windows/language-packs-for-windows-a5094319-a92d-18de-5b53-1cfc697cfca8)

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

### Weitere Einstellungen

#### Übersetzungsergebnisse in einem separaten Fenster anzeigen

Sie können die Übersetzungsergebnisse in einem separaten Fenster anzeigen.  
Öffnen Sie dazu den Einstellungsbildschirm und wählen Sie im Tab "Allgemeine Einstellungen" unter "Übersetzungsergebnisanzeigemodus" die Option "Capture Window" aus. Klicken Sie auf "OK", um den Einstellungsbildschirm zu schließen.
![Anzeigemodus Einstellungen](images/settings_window.png)

Wenn Sie die gewünschte Anwendung auswählen, werden die Übersetzungsergebnisse in einem separaten Fenster angezeigt.  
![Fenstermodus](images/window_mode.png)

#### Übersetzen von Fenstern bestimmter Anwendungen immer

Sie können einstellen, dass WindowTranslator eine bestimmte Anwendung erkennt, wenn sie gestartet wird, und automatisch mit der Übersetzung beginnt.

1. Starten Sie `WindowTranslator.exe` und öffnen Sie den Einstellungsbildschirm.  
  ![Einstellungen](images/settings.png)
2. Klicken Sie auf der Registerkarte "SettingsViewModel" auf die Schaltfläche "Ausführen" neben "Register to startup command", um die Anwendung beim Anmelden automatisch zu starten.   
  ![Startup-Registrierung](images/startup.png)
3. Geben Sie den Prozessnamen der Anwendung, die Sie übersetzen möchten, in das Feld "Automatische Übersetzung Ziel" auf der Registerkarte "Allgemeine Einstellungen" ein.  
  ![Automatisches Übersetzungsziel](images/always_translate.png)
  * Wenn Sie die Option "Automatisch übersetzen, wenn der ausgewählte Prozess gestartet wird" aktivieren, wird der Prozess automatisch als Übersetzungsziel registriert.
4. Nach Abschluss der Einstellungen klicken Sie auf die Schaltfläche "OK", um den Einstellungsbildschirm zu schließen.
5. Von nun an wird bei jedem Start des Zielprozesses eine Benachrichtigung angezeigt, die Sie fragt, ob Sie die Übersetzung starten möchten.  
  ![Benachrichtigung](images/notify.png)

##### Wenn die Benachrichtigung nicht angezeigt wird

Wenn die Benachrichtigung nicht angezeigt wird, kann es sein, dass "Fokus-Assistent" aktiviert ist.   
Folgen Sie diesen Schritten, um Benachrichtigungen zu aktivieren:

1. Öffnen Sie die Einstellungen für "Benachrichtigungen" unter "System" in den Windows-Einstellungen.   
 ![Einstellungen](images/win_settings.png)
2. Wählen Sie "Fokus-Assistent automatisch einschalten" und deaktivieren Sie das Kontrollkästchen "Wenn ich eine App im Vollbildmodus verwende".  
  ![Fokus-Assistent](images/full.png)
3. Klicken Sie unter "Prioritätsbenachrichtigungen einstellen" auf "App hinzufügen".  
 ![Benachrichtigungseinstellungen](images/notification.png)
 ![Prioritätsbenachrichtigungen](images/priority.png)
4. Wählen Sie "WindowTranslator" aus.   
  ![App auswählen](images/select_app.png)

> Translated with ChatGPT