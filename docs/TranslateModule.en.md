# Translation Modules

WindowTranslator allows you to choose from multiple translation modules.  
Each module has its own characteristics, and by selecting the appropriate module for your use case, you can use translation more comfortably.

## Bergamot ![Default](https://img.shields.io/badge/Default-brightgreen)

A machine translation module that works offline.

### Advantages
- **Completely Free**: No charges whatsoever
- **No Translation Limits**: You can translate as many times as you want
- **Fast**: Translation is quick as it's processed locally
- **Privacy**: No internet connection required, data is not sent externally
- **Stability**: Not affected by network conditions

### Disadvantages
- **Translation Accuracy**: Lower translation accuracy compared to cloud-based services
- **Memory Usage**: Uses a certain amount of memory for translation processing
- **Language Support**: Only some language pairs are supported

### Recommended Use Cases
- When you want to use it for free
- Use in offline environments
- When privacy is important
- When translating frequently

---

## Google Translate

A translation module using Google's translation service.

### Advantages
- **Completely Free**: Can be used without an API key
- **Multilingual Support**: Supports many language pairs
- **Easy**: No special configuration required

### Disadvantages
- **Translation Limits**: Limited number of characters that can be translated per day
- **Translation Accuracy**: May be less accurate compared to other paid services
- **Speed**: Affected by network conditions
- **Stability**: May suddenly become unavailable due to usage restrictions

### Recommended Use Cases
- Infrequent use
- When you want to start using immediately
- When you want to translate various language pairs

---

## DeepL

A module using DeepL's translation service, known for high-quality translations.

### Advantages
- **High Accuracy**: Provides natural, high-quality translations
- **Generous Free Tier**: Up to 500,000 characters per month for free (Free API)
- **Fast**: Quick translation processing
- **Glossary Support**: Can maintain translation consistency using glossaries

### Disadvantages
- **API Registration Required**: Requires DeepL API registration and API key setup
- **Free Tier Limits**: Migration to paid plan required when exceeding free tier
- **Language Support**: Limited language support compared to Google and others

### Recommended Use Cases
- When high-quality translation is required
- Moderate frequency of use

---

## Google AI (Gemini)

A translation module leveraging Google's latest AI technology.

### Advantages
- **Highest Accuracy**: Capable of very high-quality translation with contextual understanding
- **Flexibility**: Can customize prompts to adjust translation style
- **Glossary Support**: Can maintain translation consistency using glossaries

### Disadvantages
- **API Key Required**: Requires API key acquisition and setup from Google AI Studio
- **Pay-per-use**: Charges based on usage (though minimal)
- **Speed**: Takes longer processing time than other modules due to LLM base

### Recommended Use Cases
- When highest quality translation is required
- When customized translation style is needed
- When context-aware translation is important

---

## ChatGPT API (OR Local LLM)

A translation module using ChatGPT API or local LLM.

### Advantages
- **Highest Accuracy**: High-quality translation by large language models
- **Flexibility**: Can customize prompts to adjust translation style
- **Glossary Support**: Can maintain translation consistency using glossaries
- **Local LLM Support**: Can also use your own LLM server

### Disadvantages
- **API Key Required**: Requires API key setup for each service (except local LLM)
- **Pay-per-use**: Charges based on usage (except local LLM)
- **Speed**: Longer processing time
- **Local LLM Requirements**: High-spec PC required when running your own LLM

### Recommended Use Cases
- When highest quality translation is required
- When customized translation style is needed
- When privacy is important while wanting high-quality translation (local LLM)

---

## PLaMo

A translation module using local LLM specialized for Japanese.

### Advantages
- **Japanese Specialized**: Optimized for Japanese translation
- **Completely Free**: Open source model with no charges
- **Privacy**: Runs locally, data is not sent externally
- **Offline**: No internet connection required

### Disadvantages
- **High-spec Requirements**: Requires high-performance PC including GPU
- **Memory Usage**: Requires large amount of memory (8GB or more recommended)
- **Speed**: Processing takes time without GPU

### Recommended Use Cases
- When you own a high-performance PC
- When privacy is the top priority
- When Japanese translation quality is important

---

## How to Choose a Module

| Purpose                        | Recommended Module                           |
| ------------------------------ | -------------------------------------------- |
| Start using immediately        | **Bergamot** or **Google Translate**        |
| Highest quality translation    | **Google AI** or **ChatGPT API**            |
| Keep costs down                | **Bergamot** or **DeepL (within free tier)** |
| Privacy focused                | **Bergamot** or **PLaMo**                   |
| High frequency usage           | **Bergamot** or **DeepL**                   |
