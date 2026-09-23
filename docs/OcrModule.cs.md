# Moduly OCR

WindowTranslator umožňuje vybrat z několika modulů OCR pro rozpoznávání textu na obrazovce.  
Každý modul má své vlastnosti a výběr vhodného modulu pro váš případ použití umožní přesnější rozpoznávání textu.

## Nové rozpoznávání znaků Windows (Beta) ![Výchozí](https://img.shields.io/badge/Výchozí-brightgreen)

Místní modul OCR dodaný společností Microsoft.

### Výhody
- **Přesnost rozpoznávání**: Může se pochlubit nejvyšší přesností rozpoznávání
- **Rychlost**: Velmi vysoká rychlost zpracování

### Nevýhody
- **Využití paměti**: Může použít více než 1 GB paměti pouze pro zpracování rozpoznávání
- **Provozní prostředí**: Nemusí fungovat v některých prostředích (doporučeno Windows 10 nebo novější)

---

## Standardní rozpoznávání znaků Windows

OCR engine, který je standardem v systému Windows 10 a novějším.

### Výhody
- **Využití paměti**: Lehký s nízkým využitím paměti
- **Provozní prostředí**: Široce dostupný v systému Windows 10 a novějším

### Nevýhody
- **Přesnost rozpoznávání**: Může být slabší u složitých písem nebo ručně psaného textu
- **Nastavení**: Může být vyžadována ruční instalace jazykových dat

---

## Tesseract OCR

OCR engine s otevřeným zdrojovým kódem.

### Výhody
- **Vícejazyčná podpora**: Podporuje více než 100 jazyků
- **Stabilita**: Spolehlivý engine s dlouhou historií

### Nevýhody
- **Přesnost rozpoznávání**: Může být horší ve srovnání s jinými OCR

---

## Výběr modulu

Vyberte prosím modul, který funguje v následujícím pořadí vysoké přesnosti rozpoznávání:

1. Nové rozpoznávání znaků Windows (Beta)
2. Standardní rozpoznávání znaků Windows
3. Tesseract OCR
