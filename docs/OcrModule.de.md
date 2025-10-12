# OCR-Module

WindowTranslator ermöglicht Ihnen die Auswahl aus mehreren OCR-Modulen zur Erkennung von Text auf dem Bildschirm.  
Jedes Modul hat seine eigenen Eigenschaften, und die Wahl des geeigneten Moduls für Ihren Anwendungsfall ermöglicht eine genauere Texterkennung.

## OneOcr ![Standard](https://img.shields.io/badge/Standard-brightgreen)

Ein lokales OCR-Modul von Microsoft.

### Vorteile
- **Völlig kostenlos**: Keinerlei Gebühren
- **Schnell**: Schnelle Erkennung durch lokale Verarbeitung
- **Datenschutz**: Daten werden nicht extern gesendet
- **Offline**: Keine Internetverbindung erforderlich
- **Stabilität**: Nicht von Netzwerkbedingungen betroffen
- **Leichtgewichtig**: Geringer Speicherverbrauch

### Nachteile
- **Erkennungsgenauigkeit**: Geringere Genauigkeit bei komplexen Schriftarten oder handgeschriebenem Text
- **Sprachunterstützung**: Nur begrenzte Sprachen unterstützt
- **Sonderzeichen**: Kann bei dekorativen Zeichen oder speziellen Layouts schwach sein

### Empfohlene Anwendungsfälle
- Erkennung von Standardschrift-Text
- Wenn Datenschutz Priorität hat
- Offline-Umgebungsnutzung
- Nutzung mit Low-Spec-PC

---

## Tesseract OCR

Eine Open-Source-OCR-Engine.

### Vorteile
- **Völlig kostenlos**: Open-Source und kostenlos nutzbar
- **Mehrsprachige Unterstützung**: Unterstützt über 100 Sprachen
- **Anpassbarkeit**: Kann durch Hinzufügen von Trainingsdaten angepasst werden
- **Offline**: Keine Internetverbindung erforderlich
- **Stabilität**: Zuverlässige Engine mit langer Geschichte

### Nachteile
- **Erkennungsgenauigkeit**: Geringere Genauigkeit im Vergleich zu neuesten KI-basierten OCR
- **Einrichtung**: Installation von Sprachdaten erforderlich
- **Geschwindigkeit**: Relativ langsame Verarbeitungsgeschwindigkeit
- **Bilder schlechter Qualität**: Schwach bei unscharfen oder verrauschten Bildern

### Empfohlene Anwendungsfälle
- Erkennung von Text in verschiedenen Sprachen
- Wenn Anpassung benötigt wird
- Sprachspezifische Erkennung

---

## Google AI OCR (Gemini Vision)

Ein OCR-Modul, das Googles KI-Technologie nutzt.

### Vorteile
- **Höchste Genauigkeit**: Sehr hohe Erkennungsgenauigkeit mit KI-Technologie
- **Handschriftunterstützung**: Kann handgeschriebenen Text erkennen
- **Komplexe Layouts**: Erkennt komplexe Layouts genau
- **Mehrsprachige Unterstützung**: Unterstützt breites Spektrum an Sprachen
- **Bildqualitätstoleranz**: Hohe Erkennungsgenauigkeit auch bei Bildern schlechter Qualität
- **Kontextverständnis**: Erkennung unter Berücksichtigung des Kontexts

### Nachteile
- **API-Schlüssel erforderlich**: Registrierung bei Google Cloud Platform und API-Schlüssel-Einrichtung notwendig
- **Nutzungsabhängige Bezahlung**: Gebühren basierend auf Nutzung (kostenloses Kontingent verfügbar)
- **Geschwindigkeit**: Braucht Zeit zur Verarbeitung über Netzwerk
- **Datenschutz**: Bilddaten werden an Google-Server gesendet
- **Nur online**: Internetverbindung erforderlich

### Empfohlene Anwendungsfälle
- Erkennung von handgeschriebenem oder dekorativem Text
- Wenn hohe Erkennungsgenauigkeit benötigt wird
- Erkennung von Text mit komplexen Layouts
- Wenn Bildqualität niedrig ist

---

## LLM OCR

Ein OCR-Modul, das Vision-Fähigkeiten großer Sprachmodelle (LLM) verwendet.

### Vorteile
- **Höchste Genauigkeit**: Sehr hohe Erkennungsgenauigkeit mit neuester KI-Technologie
- **Kontextverständnis**: Erkennung unter Berücksichtigung des gesamten Bildkontexts
- **Flexibilität**: Unterstützt komplexe Layouts und spezielle Schriftarten
- **Mehrsprachige Unterstützung**: Unterstützt breites Spektrum an Sprachen
- **Argumentationsfähigkeit**: Nicht nur Zeichenerkennung, sondern verständnisbasierte Erkennung

### Nachteile
- **API-Schlüssel erforderlich**: Benötigt API-Schlüssel von OpenAI, Anthropic usw.
- **Nutzungsabhängige Bezahlung**: Gebühren basierend auf Nutzung
- **Geschwindigkeit**: Längere Verarbeitungszeit
- **Kosten**: Höhere Kosten aufgrund hohen Token-Verbrauchs für Bildverarbeitung
- **Datenschutz**: Bilddaten werden an externe Dienste gesendet

### Empfohlene Anwendungsfälle
- Wenn höchste Erkennungsqualität benötigt wird
- Komplexer Text, den herkömmliche OCR nicht erkennen kann
- Wenn kontextbewusste Erkennung notwendig ist

---

## Ein Modul auswählen

| Zweck | Empfohlenes Modul |
|-------|-------------------|
| Sofort mit der Nutzung beginnen | **OneOcr** |
| Höchste Erkennungsqualität benötigt | **Google AI OCR** oder **LLM OCR** |
| Kosten niedrig halten | **OneOcr** oder **Tesseract** |
| Datenschutz-Priorität | **OneOcr** oder **Tesseract** |
| Mehrsprachige Unterstützung benötigt | **Tesseract** oder **Google AI OCR** |
| Handschrifterkennung | **Google AI OCR** oder **LLM OCR** |
| Offline-Umgebung | **OneOcr** oder **Tesseract** |

---

## Tipps zur Verbesserung der OCR-Genauigkeit

Unabhängig davon, welches OCR-Modul Sie verwenden, können Sie die Erkennungsgenauigkeit verbessern, indem Sie auf Folgendes achten:

1. **Bildschirmauflösung**: Höhere Auflösung verbessert die Erkennungsgenauigkeit
2. **Schriftgröße**: Zu kleine Schriften sind schwer zu erkennen, passen Sie auf geeignete Größe an
3. **Kontrast**: Höherer Kontrast zwischen Text und Hintergrund verbessert die Erkennungsgenauigkeit
4. **Klare Anzeige**: Streben Sie Anzeige ohne Unschärfe oder Verzerrung an
5. **Spracheinstellungen**: Stellen Sie die Erkennungszielsprache korrekt ein
