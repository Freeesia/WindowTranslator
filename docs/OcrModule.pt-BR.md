# Módulo OCRs

WindowTranslator allows you to choose de multiple OCR modules for recognizing text on screen.  
Each module has its own characteristics, and selecting the appropriate module for your use case will enable more accurate text recognition.

## New Windows Character Recognition (Beta) ![Padrão](https://img.shields.io/badge/Padrão-brightgreen)

A local OCR module provided by Microsoft.

### Advantages
- **Recognition Accuracy**: Boasts the highest recognition accuracy
- **Fast**: Very fast processing speed

### Disadvantages
- **Memory Usage**: May use over 1GB of memory just for recognition processing
- **Operating Environment**: May not work in some environments (Windows 10 or later recommended)

---

## Windows Standard Character Recognition

The OCR engine that comes standard with Windows 10 and later.

### Advantages
- **Memory Usage**: Lightweight with low memory usage
- **Operating Environment**: Widely available on Windows 10 and later

### Disadvantages
- **Recognition Accuracy**: May be weak with complex fonts or handwritten text
- **Setup**: Manual installation of language data may be required

---

## Tesseract OCR

An open-source OCR engine.

### Advantages
- **Multilingual Support**: Supports over 100 languages
- **Stability**: Reliable engine with long history

### Disadvantages
- **Recognition Accuracy**: May be inferior compared to other OCR

---

## Choosing a Module

Please select the module that works in the following order of high recognition accuracy:

1. New Windows Character Recognition (Beta)
2. Windows Standard Character Recognition
3. Tesseract OCR
