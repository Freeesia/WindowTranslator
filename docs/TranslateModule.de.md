# Übersetzungsmodule

WindowTranslator ermöglicht Ihnen die Auswahl aus mehreren Übersetzungsmodulen.  
Jedes Modul hat seine eigenen Eigenschaften, und die Wahl des geeigneten Moduls für Ihren Anwendungsfall wird Ihnen ein komfortableres Übersetzungserlebnis ermöglichen.

## Bergamot ![Standard](https://img.shields.io/badge/Standard-brightgreen)

Ein Offline-Maschinenübersetzungsmodul.

### Vorteile
- **Völlig kostenlos**: Keinerlei Gebühren
- **Keine Übersetzungsbegrenzung**: Übersetzen Sie so oft Sie möchten
- **Schnell**: Schnelle Übersetzungen durch lokale Verarbeitung
- **Datenschutz**: Keine Internetverbindung erforderlich, Daten werden nicht extern gesendet
- **Stabilität**: Nicht von Netzwerkbedingungen betroffen

### Nachteile
- **Übersetzungsgenauigkeit**: Geringere Genauigkeit im Vergleich zu Cloud-basierten Diensten
- **Speichernutzung**: Benötigt 1 GB oder mehr freien Speicher
- **Sprachunterstützung**: Nur bestimmte Sprachpaare werden unterstützt

### Empfohlene Anwendungsfälle
- Umgebungen mit instabiler Internetverbindung
- Wenn Datenschutz Priorität hat
- Häufige Übersetzungsnutzung

---

## Google Translate

Ein Übersetzungsmodul, das Googles Übersetzungsdienst verwendet.

### Vorteile
- **Völlig kostenlos**: Kann ohne API-Schlüssel verwendet werden
- **Mehrsprachige Unterstützung**: Unterstützt viele Sprachpaare
- **Einfach**: Keine spezielle Konfiguration erforderlich

### Nachteile
- **Übersetzungsbegrenzung**: Begrenzte Anzahl von Zeichen, die pro Tag übersetzt werden können
- **Übersetzungsgenauigkeit**: In einigen Fällen geringere Genauigkeit im Vergleich zu anderen kostenpflichtigen Diensten
- **Geschwindigkeit**: Von Netzwerkbedingungen betroffen
- **Stabilität**: Kann aufgrund von Nutzungsbeschränkungen plötzlich nicht verfügbar werden

### Empfohlene Anwendungsfälle
- Geringe Nutzungshäufigkeit
- Wenn Sie sofort mit der Nutzung beginnen möchten
- Wenn Sie verschiedene Sprachpaare übersetzen müssen

---

## DeepL

Ein Modul, das DeepLs Übersetzungsdienst verwendet, bekannt für hochwertige Übersetzungen.

### Vorteile
- **Hohe Genauigkeit**: Natürliche und qualitativ hochwertige Übersetzungen
- **Großzügiges kostenloses Kontingent**: Bis zu 500.000 Zeichen pro Monat kostenlos (Free API)
- **Schnell**: Schnelle Übersetzungsverarbeitung
- **Geschäftliche Nutzung**: Hochwertige Übersetzungen auch für spezialisierte Dokumente

### Nachteile
- **API-Registrierung erforderlich**: DeepL API-Registrierung und API-Schlüssel-Einrichtung notwendig
- **Begrenzung des kostenlosen Kontingents**: Migration zu kostenpflichtigem Plan erforderlich, wenn kostenloses Kontingent überschritten wird
- **Sprachunterstützung**: Begrenztere Sprachunterstützung im Vergleich zu Google

### Empfohlene Anwendungsfälle
- Wenn hochwertige Übersetzungen erforderlich sind
- Übersetzung von Geschäftsdokumenten
- Mittlere Nutzungshäufigkeit

---

## Google AI (Gemini)

Ein Übersetzungsmodul, das Googles neueste KI-Technologie nutzt.

### Vorteile
- **Höchste Genauigkeit**: Sehr hochwertige Übersetzungen mit Kontextverständnis
- **Flexible Ausdrücke**: Übersetzt mit natürlicher Formulierung
- **Fachterminologie**: Unterstützt technische Dokumente, Spiele und spezialisierte Inhalte
- **Kostenloses Kontingent**: Kann bis zu einer bestimmten Menge kostenlos genutzt werden

### Nachteile
- **API-Schlüssel erforderlich**: Registrierung bei Google Cloud Platform und API-Schlüssel-Einrichtung notwendig
- **Nutzungsabhängige Bezahlung**: Gebühren fallen nach Überschreitung des kostenlosen Kontingents an (jedoch minimal)
- **Geschwindigkeit**: Längere Verarbeitung als andere Module aufgrund der LLM-Basis

### Empfohlene Anwendungsfälle
- Wenn die höchste Übersetzungsqualität erforderlich ist
- Übersetzung spezialisierter Inhalte wie Spiele oder technische Dokumente
- Wenn kontextbewusste Übersetzung notwendig ist

---

## LLM-Plugin (ChatGPT/Claude/Lokales LLM)

Ein Übersetzungsmodul, das OpenAI, Anthropic oder lokale LLMs verwendet.

### Vorteile
- **Höchste Genauigkeit**: Hochwertige Übersetzungen mit großen Sprachmodellen
- **Flexibilität**: Passen Sie Prompts an, um den Übersetzungsstil anzupassen
- **Kontextverständnis**: Übersetzungen unter Berücksichtigung längeren Kontexts
- **Lokale LLM-Unterstützung**: Kann eigenen LLM-Server verwenden

### Nachteile
- **API-Schlüssel erforderlich**: API-Schlüssel-Einrichtung für jeden Dienst notwendig (außer lokales LLM)
- **Nutzungsabhängige Bezahlung**: Gebühren basierend auf Nutzung (außer lokales LLM)
- **Geschwindigkeit**: Längere Verarbeitungszeit
- **Lokale LLM-Anforderungen**: Hochleistungs-PC erforderlich für eigenes LLM

### Empfohlene Anwendungsfälle
- Wenn die höchste Übersetzungsqualität erforderlich ist
- Wenn angepasster Übersetzungsstil benötigt wird
- Wenn Datenschutz Priorität hat, während hochwertige Übersetzungen gewünscht sind (lokales LLM)

---

## PLaMo

Ein Übersetzungsmodul, das lokales LLM spezialisiert für Japanisch verwendet.

### Vorteile
- **Japanisch-spezialisiert**: Optimiert für japanische Übersetzung
- **Völlig kostenlos**: Keine Gebühren mit Open-Source-Modell
- **Datenschutz**: Läuft lokal, Daten werden nicht extern gesendet
- **Offline**: Keine Internetverbindung erforderlich

### Nachteile
- **Hohe Spezifikationsanforderungen**: Benötigt Hochleistungs-PC einschließlich GPU
- **Einrichtung**: Komplexe Erstkonfiguration
- **Speichernutzung**: Benötigt große Menge an Speicher (8GB+ empfohlen)
- **Geschwindigkeit**: Braucht Zeit zur Verarbeitung ohne GPU

### Empfohlene Anwendungsfälle
- Wenn Sie einen Hochleistungs-PC besitzen
- Wenn Datenschutz höchste Priorität hat
- Wenn japanische Übersetzungsqualität Priorität hat

---

## Ein Modul auswählen

| Zweck | Empfohlenes Modul |
|-------|-------------------|
| Sofort mit der Nutzung beginnen | **Bergamot** oder **Google Translate** |
| Höchste Übersetzungsqualität benötigt | **Google AI** oder **LLM-Plugin** |
| Kosten niedrig halten | **Bergamot** oder **DeepL (innerhalb kostenloses Kontingent)** |
| Datenschutz-Priorität | **Bergamot** oder **PLaMo** |
| Häufige Nutzung | **Bergamot** oder **DeepL** |
| Geschäftliche Nutzung | **DeepL** oder **Google AI** |
