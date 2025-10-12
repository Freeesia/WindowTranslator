# OCR Modules

WindowTranslator allows you to choose from multiple OCR modules for recognizing text on screen.  
Each module has its own characteristics, and selecting the appropriate module for your use case will enable more accurate text recognition.

## OneOcr ![Default](https://img.shields.io/badge/Default-brightgreen)

A local OCR module provided by Microsoft.

### Advantages
- **Completely Free**: No fees whatsoever
- **Fast**: Quick recognition as it processes locally
- **Privacy**: Data is not sent externally
- **Offline**: No internet connection required
- **Stability**: Not affected by network conditions
- **Lightweight**: Low memory usage

### Disadvantages
- **Recognition Accuracy**: Lower accuracy for complex fonts or handwritten text
- **Language Support**: Only limited languages supported
- **Special Characters**: May be weak with decorative characters or special layouts

### Recommended Use Cases
- Recognition of standard font text
- When privacy is a priority
- Offline environment usage
- Low-spec PC usage

---

## Tesseract OCR

An open-source OCR engine.

### Advantages
- **Completely Free**: Open-source and free to use
- **Multilingual Support**: Supports over 100 languages
- **Customizability**: Can be customized by adding training data
- **Offline**: No internet connection required
- **Stability**: Reliable engine with long history

### Disadvantages
- **Recognition Accuracy**: Lower accuracy compared to latest AI-based OCR
- **Setup**: Language data installation required
- **Speed**: Relatively slow processing speed
- **Low-Quality Images**: Weak with blurry or noisy images

### Recommended Use Cases
- Recognition of text in various languages
- When customization is needed
- Language-specific recognition

---

## Google AI OCR (Gemini Vision)

An OCR module leveraging Google's AI technology.

### Advantages
- **Highest Accuracy**: Very high recognition accuracy with AI technology
- **Handwriting Support**: Can recognize handwritten text
- **Complex Layouts**: Accurately recognizes complex layouts
- **Multilingual Support**: Supports wide range of languages
- **Image Quality Tolerance**: High recognition accuracy even with low-quality images
- **Context Understanding**: Recognition considering context

### Disadvantages
- **API Key Required**: Registration with Google Cloud Platform and API key setup necessary
- **Pay-as-you-go**: Charges based on usage (free tier available)
- **Speed**: Takes time to process via network
- **Privacy**: Image data is sent to Google servers
- **Online Only**: Internet connection required

### Recommended Use Cases
- Recognition of handwritten or decorative text
- When high recognition accuracy is needed
- Recognition of text with complex layouts
- When image quality is low

---

## LLM OCR

An OCR module using vision capabilities of large language models (LLM).

### Advantages
- **Highest Accuracy**: Very high recognition accuracy with latest AI technology
- **Context Understanding**: Recognition considering entire image context
- **Flexibility**: Supports complex layouts and special fonts
- **Multilingual Support**: Supports wide range of languages
- **Reasoning Ability**: Not just character recognition, but understanding-based recognition

### Disadvantages
- **API Key Required**: Requires API keys from OpenAI, Anthropic, etc.
- **Pay-as-you-go**: Charges based on usage
- **Speed**: Longer processing time
- **Cost**: Higher cost due to high token consumption for image processing
- **Privacy**: Image data is sent to external services

### Recommended Use Cases
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
