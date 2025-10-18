# OCR-Module

WindowTranslator ermöglicht Ihnen die Auswahl aus mehreren OCR-Modulen zur Erkennung von Text auf dem Bildschirm.  
Jedes Modul hat seine eigenen Eigenschaften, und die Wahl des geeigneten Moduls für Ihren Anwendungsfall ermöglicht eine genauere Texterkennung.

## Neue Windows-Zeichenerkennung (Beta) ![Standard](https://img.shields.io/badge/Standard-brightgreen)

Ein lokales OCR-Modul von Microsoft.

### Vorteile
- **Erkennungsgenauigkeit**: Verfügt über die höchste Erkennungsgenauigkeit
- **Schnell**: Sehr hohe Verarbeitungsgeschwindigkeit

### Nachteile
- **Speicherverbrauch**: Kann allein für die Erkennung über 1 GB Speicher verwenden
- **Betriebsumgebung**: Funktioniert möglicherweise nicht in einigen Umgebungen (Windows 10 oder höher empfohlen)

---

### Windows-Standard-Zeichenerkennung

Die OCR-Engine, die standardmäßig in Windows 10 und höher integriert ist.

### Vorteile
- **Speicherverbrauch**: Leichtgewichtig mit geringem Speicherverbrauch
- **Betriebsumgebung**: Weitgehend verfügbar unter Windows 10 und höher

### Nachteile
- **Erkennungsgenauigkeit**: Kann bei komplexen Schriftarten oder handgeschriebenem Text schwach sein
- **Einrichtung**: Manuelle Installation von Sprachdaten kann erforderlich sein

---

## Tesseract OCR

Eine Open-Source-OCR-Engine.

### Vorteile
- **Mehrsprachige Unterstützung**: Unterstützt über 100 Sprachen
- **Stabilität**: Zuverlässige Engine mit langer Geschichte

### Nachteile
- **Erkennungsgenauigkeit**: Kann im Vergleich zu anderen OCR unterlegen sein

---

## Auswahl eines Moduls

Bitte wählen Sie das Modul aus, das in folgender Reihenfolge hoher Erkennungsgenauigkeit funktioniert:

1. OneOcr
2. Windows-Standard-Zeichenerkennung
3. Tesseract OCR
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
