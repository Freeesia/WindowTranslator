# OCR Modules

WindowTranslator allows you to choose from multiple OCR modules for recognizing text on screen.  
Each module has its own characteristics, and selecting the appropriate module for your use case will enable more accurate text recognition.

## New Windows Character Recognition (Beta) ![Default](https://img.shields.io/badge/Default-brightgreen)

A local OCR module provided by Microsoft.

### Advantages
- **Recognition Accuracy**: Boasts the highest recognition accuracy
- **Fast**: Very fast processing speed

### Disadvantages
- **Memory Usage**: May use over 1GB of memory just for recognition processing
- **Operating Environment**: May not work in some environments (Windows 10 or later recommended)

---

### Windows Standard Character Recognition

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

1. OneOcr
2. Windows Standard Character Recognition
3. Tesseract OCR
- When highest quality recognition is needed
- Complex text that conventional OCR cannot recognize
- When context-aware recognition is necessary

---

## Choosing a Module

| Purpose | Recommended Module |
|---------|-------------------|
| Start using immediately | **OneOcr** |
| Highest quality recognition needed | **Google AI OCR** or **LLM OCR** |
| Want to keep costs down | **OneOcr** or **Tesseract** |
| Privacy priority | **OneOcr** or **Tesseract** |
| Multilingual support needed | **Tesseract** or **Google AI OCR** |
| Handwriting recognition | **Google AI OCR** or **LLM OCR** |
| Offline environment | **OneOcr** or **Tesseract** |

---

## Tips for Improving OCR Accuracy

Regardless of which OCR module you use, you can improve recognition accuracy by paying attention to the following:

1. **Screen Resolution**: Higher resolution improves recognition accuracy
2. **Font Size**: Too small fonts are difficult to recognize, adjust to appropriate size
3. **Contrast**: Higher contrast between text and background improves recognition accuracy
4. **Clear Display**: Aim for display without blur or distortion
5. **Language Settings**: Set the recognition target language correctly
