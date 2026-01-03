# Moduły OCR

WindowTranslator pozwala wybrać spośród wielu modułów OCR do rozpoznawania tekstu na ekranie.  
Każdy moduł ma swoje własne cechy, a wybór odpowiedniego modułu dla Twojego przypadku użycia umożliwi dokładniejsze rozpoznawanie tekstu.

## Nowe rozpoznawanie znaków Windows (Beta) ![Domyślny](https://img.shields.io/badge/Domyślny-brightgreen)

Lokalny moduł OCR dostarczony przez Microsoft.

### Zalety
- **Dokładność rozpoznawania**: Może pochwalić się najwyższą dokładnością rozpoznawania
- **Szybkość**: Bardzo szybka prędkość przetwarzania

### Wady
- **Użycie pamięci**: Może używać ponad 1GB pamięci tylko do przetwarzania rozpoznawania
- **Środowisko operacyjne**: Może nie działać w niektórych środowiskach (zalecany Windows 10 lub nowszy)

---

## Standardowe rozpoznawanie znaków Windows

Silnik OCR, który jest standardem w Windows 10 i nowszych.

### Zalety
- **Użycie pamięci**: Lekki z niskim zużyciem pamięci
- **Środowisko operacyjne**: Szeroko dostępny w Windows 10 i nowszych

### Wady
- **Dokładność rozpoznawania**: Może być słaby przy złożonych czcionkach lub tekście odręcznym
- **Konfiguracja**: Może być wymagana ręczna instalacja danych językowych

---

## Tesseract OCR

Silnik OCR o otwartym kodzie źródłowym.

### Zalety
- **Wsparcie wielojęzyczne**: Obsługuje ponad 100 języków
- **Stabilność**: Niezawodny silnik z długą historią

### Wady
- **Dokładność rozpoznawania**: Może być gorsza w porównaniu z innymi OCR

---

## Wybór modułu

Proszę wybrać moduł, który działa w następującej kolejności wysokiej dokładności rozpoznawania:

1. Nowe rozpoznawanie znaków Windows (Beta)
2. Standardowe rozpoznawanie znaków Windows
3. Tesseract OCR
