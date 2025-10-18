# Übersetzungsmodule

WindowTranslator ermöglicht Ihnen die Auswahl aus mehreren Übersetzungsmodulen.  
Jedes Modul hat seine eigenen Eigenschaften, und durch die Wahl des geeigneten Moduls für Ihren Anwendungsfall können Sie Übersetzungen komfortabler nutzen.

## Bergamot ![Standard](https://img.shields.io/badge/Standard-brightgreen)

Ein Maschinenübersetzungsmodul, das offline funktioniert.

### Vorteile
- **Völlig kostenlos**: Keinerlei Kosten
- **Keine Übersetzungslimits**: Sie können beliebig oft übersetzen
- **Schnell**: Übersetzung ist schnell, da sie lokal verarbeitet wird
- **Datenschutz**: Keine Internetverbindung erforderlich, Daten werden nicht extern gesendet
- **Stabilität**: Nicht von Netzwerkbedingungen betroffen

### Nachteile
- **Übersetzungsgenauigkeit**: Geringere Übersetzungsgenauigkeit im Vergleich zu Cloud-basierten Diensten
- **Speichernutzung**: Verwendet eine bestimmte Menge Speicher für die Übersetzungsverarbeitung
- **Sprachunterstützung**: Nur einige Sprachpaare werden unterstützt

### Empfohlene Anwendungsfälle
- Wenn Sie es kostenlos nutzen möchten
- Nutzung in Offline-Umgebungen
- Wenn Datenschutz wichtig ist
- Bei häufigem Übersetzen

---

## Google Translate

Ein Übersetzungsmodul, das Googles Übersetzungsdienst verwendet.

### Vorteile
- **Völlig kostenlos**: Kann ohne API-Schlüssel verwendet werden
- **Mehrsprachige Unterstützung**: Unterstützt viele Sprachpaare
- **Einfach**: Keine spezielle Konfiguration erforderlich

### Nachteile
- **Übersetzungslimits**: Begrenzte Anzahl von Zeichen, die pro Tag übersetzt werden können
- **Übersetzungsgenauigkeit**: Kann im Vergleich zu anderen kostenpflichtigen Diensten weniger genau sein
- **Geschwindigkeit**: Von Netzwerkbedingungen betroffen
- **Stabilität**: Kann aufgrund von Nutzungsbeschränkungen plötzlich nicht verfügbar werden

### Empfohlene Anwendungsfälle
- Seltene Nutzung
- Wenn Sie sofort mit der Nutzung beginnen möchten
- Wenn Sie verschiedene Sprachpaare übersetzen möchten

---

## DeepL

Ein Modul, das DeepLs Übersetzungsdienst verwendet, bekannt für hochwertige Übersetzungen.

### Vorteile
- **Hohe Genauigkeit**: Liefert natürliche, hochwertige Übersetzungen
- **Großzügiges kostenloses Kontingent**: Bis zu 500.000 Zeichen pro Monat kostenlos (Free API)
- **Schnell**: Schnelle Übersetzungsverarbeitung
- **Glossar-Unterstützung**: Kann Übersetzungskonsistenz durch Glossare aufrechterhalten

### Nachteile
- **API-Registrierung erforderlich**: Erfordert DeepL API-Registrierung und API-Schlüssel-Einrichtung
- **Begrenzung des kostenlosen Kontingents**: Migration zu kostenpflichtigem Plan erforderlich bei Überschreitung des kostenlosen Kontingents
- **Sprachunterstützung**: Begrenzte Sprachunterstützung im Vergleich zu Google und anderen

### Empfohlene Anwendungsfälle
- Wenn hochwertige Übersetzung erforderlich ist
- Mittlere Nutzungshäufigkeit

---

## Google AI (Gemini)

Ein Übersetzungsmodul, das Googles neueste KI-Technologie nutzt.

### Vorteile
- **Höchste Genauigkeit**: Fähig zu sehr hochwertiger Übersetzung mit Kontextverständnis
- **Flexibilität**: Kann Prompts anpassen, um Übersetzungsstil zu justieren
- **Glossar-Unterstützung**: Kann Übersetzungskonsistenz durch Glossare aufrechterhalten

### Nachteile
- **API-Schlüssel erforderlich**: Erfordert API-Schlüssel-Beschaffung und Einrichtung von Google AI Studio
- **Nutzungsabhängige Bezahlung**: Gebühren basierend auf Nutzung (jedoch minimal)
- **Geschwindigkeit**: Längere Verarbeitungszeit als andere Module aufgrund der LLM-Basis

### Empfohlene Anwendungsfälle
- Wenn höchste Übersetzungsqualität erforderlich ist
- Wenn angepasster Übersetzungsstil benötigt wird
- Wenn kontextbewusste Übersetzung wichtig ist

---

## ChatGPT API (ODER Lokales LLM)

Ein Übersetzungsmodul, das ChatGPT API oder lokales LLM verwendet.

### Vorteile
- **Höchste Genauigkeit**: Hochwertige Übersetzung durch große Sprachmodelle
- **Flexibilität**: Kann Prompts anpassen, um Übersetzungsstil zu justieren
- **Glossar-Unterstützung**: Kann Übersetzungskonsistenz durch Glossare aufrechterhalten
- **Lokale LLM-Unterstützung**: Kann auch eigenen LLM-Server verwenden

### Nachteile
- **API-Schlüssel erforderlich**: Erfordert API-Schlüssel-Einrichtung für jeden Dienst (außer lokales LLM)
- **Nutzungsabhängige Bezahlung**: Gebühren basierend auf Nutzung (außer lokales LLM)
- **Geschwindigkeit**: Längere Verarbeitungszeit
- **Lokale LLM-Anforderungen**: Hochleistungs-PC erforderlich beim Betrieb eigenes LLM

### Empfohlene Anwendungsfälle
- Wenn höchste Übersetzungsqualität erforderlich ist
- Wenn angepasster Übersetzungsstil benötigt wird
- Wenn Datenschutz wichtig ist, während hochwertige Übersetzung gewünscht wird (lokales LLM)

---

## PLaMo

Ein Übersetzungsmodul, das lokales LLM spezialisiert für Japanisch verwendet.

### Vorteile
- **Japanisch-spezialisiert**: Optimiert für japanische Übersetzung
- **Völlig kostenlos**: Open-Source-Modell ohne Gebühren
- **Datenschutz**: Läuft lokal, Daten werden nicht extern gesendet
- **Offline**: Keine Internetverbindung erforderlich

### Nachteile
- **Hohe Spezifikationsanforderungen**: Benötigt Hochleistungs-PC einschließlich GPU
- **Speichernutzung**: Benötigt große Menge an Speicher (8GB oder mehr empfohlen)
- **Geschwindigkeit**: Verarbeitung braucht Zeit ohne GPU

### Empfohlene Anwendungsfälle
- Wenn Sie einen Hochleistungs-PC besitzen
- Wenn Datenschutz höchste Priorität hat
- Wenn japanische Übersetzungsqualität wichtig ist

---

## Auswahl eines Moduls

| Zweck                          | Empfohlenes Modul                         |
| ------------------------------ | ----------------------------------------- |
| Sofort mit der Nutzung beginnen | **Bergamot** oder **Google Translate**  |
| Höchste Übersetzungsqualität  | **Google AI** oder **ChatGPT API**       |
| Kosten niedrig halten         | **Bergamot** oder **DeepL (innerhalb kostenloses Kontingent)** |
| Datenschutz im Fokus          | **Bergamot** oder **PLaMo**              |
| Häufige Nutzung               | **Bergamot** oder **DeepL**              |
